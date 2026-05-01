namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Hand-rolled recursive descent parser for the condition DSL.
/// Supports: comparisons, AND/OR/NOT, parentheses, EXISTS, COUNT, LOOKUP,
/// field references (bare, record.*, previous.*, auth.*, alias.field), and literals.
/// </summary>
public sealed class ConditionParser
{
    private static readonly HashSet<string> ComparisonOperators = new()
    {
        "==", "!=", ">=", "<=", ">", "<"
    };

    private readonly List<Token> _tokens;
    private int _pos;

    private ConditionParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    /// <summary>
    /// Parse a condition string into an AST.
    /// </summary>
    public static ConditionNode Parse(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            throw new ConditionParseException("Condition cannot be empty.");

        var tokens = Tokenize(condition);
        if (tokens.Count == 0)
            throw new ConditionParseException("Condition produced no tokens.");

        var parser = new ConditionParser(tokens);
        var node = parser.ParseOr();

        if (parser._pos < parser._tokens.Count)
        {
            var remaining = parser._tokens[parser._pos];
            throw new ConditionParseException(
                $"Unexpected token '{remaining.Value}' at position {remaining.Position}.");
        }

        return node;
    }

    // --- Grammar ---
    // or       := and (OR and)*
    // and      := unary (AND unary)*
    // unary    := NOT unary | primary
    // primary  := EXISTS ... | COUNT ... | LOOKUP ... | comparison | '(' or ')'
    // comparison := atom (compOp atom)?
    // atom     := literal | fieldRef

    private ConditionNode ParseOr()
    {
        var left = ParseAnd();
        while (MatchKeyword("OR"))
        {
            var right = ParseAnd();
            left = new LogicalNode(left, "OR", right);
        }
        return left;
    }

    private ConditionNode ParseAnd()
    {
        var left = ParseUnary();
        while (MatchKeyword("AND"))
        {
            var right = ParseUnary();
            left = new LogicalNode(left, "AND", right);
        }
        return left;
    }

    private ConditionNode ParseUnary()
    {
        if (MatchKeyword("NOT"))
        {
            var inner = ParseUnary();
            return new NotNode(inner);
        }
        return ParsePrimary();
    }

    private ConditionNode ParsePrimary()
    {
        if (_pos >= _tokens.Count)
            throw new ConditionParseException("Unexpected end of condition.");

        var current = _tokens[_pos];

        // Parenthesized expression
        if (current.Type == TokenType.LeftParen)
        {
            _pos++;
            var inner = ParseOr();
            Expect(TokenType.RightParen, "')'");
            return inner;
        }

        // EXISTS table WHERE ...
        if (current.Type == TokenType.Keyword && current.Value.Equals("EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return ParseExists();
        }

        // COUNT table WHERE ...
        if (current.Type == TokenType.Keyword && current.Value.Equals("COUNT", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCount();
        }

        // LOOKUP table.field WHERE ...
        if (current.Type == TokenType.Keyword && current.Value.Equals("LOOKUP", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLookup();
        }

        // Comparison or standalone atom
        return ParseComparison();
    }

    private ConditionNode ParseComparison()
    {
        var left = ParseAtom();

        if (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.Operator)
        {
            var op = _tokens[_pos].Value;
            _pos++;
            var right = ParseAtom();
            return new ComparisonNode(left, op, right);
        }

        return left;
    }

    private ConditionNode ParseAtom()
    {
        if (_pos >= _tokens.Count)
            throw new ConditionParseException("Unexpected end of condition; expected a value or field reference.");

        var token = _tokens[_pos];

        switch (token.Type)
        {
            case TokenType.StringLiteral:
                _pos++;
                return new LiteralNode(token.Value, $"'{token.Value}'");

            case TokenType.NumberLiteral:
                _pos++;
                if (decimal.TryParse(token.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    // Store as int if it has no fractional part
                    if (num == Math.Truncate(num) && num >= int.MinValue && num <= int.MaxValue)
                        return new LiteralNode((int)num, token.Value);
                    return new LiteralNode(num, token.Value);
                }
                return new LiteralNode(token.Value, token.Value);

            case TokenType.BooleanLiteral:
                _pos++;
                var boolVal = token.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                return new LiteralNode(boolVal, token.Value);

            case TokenType.Identifier:
                return ParseFieldReference();

            default:
                throw new ConditionParseException(
                    $"Unexpected token '{token.Value}' (type={token.Type}) at position {token.Position}.");
        }
    }

    private ConditionNode ParseFieldReference()
    {
        var token = _tokens[_pos];
        _pos++;
        var name = token.Value;

        // Check for dotted reference: prefix.field
        if (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.Dot)
        {
            _pos++; // consume dot
            if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Identifier)
                throw new ConditionParseException(
                    $"Expected field name after '{name}.' at position {token.Position}.");

            var fieldName = _tokens[_pos].Value;
            _pos++;

            if (name.Equals("previous", StringComparison.OrdinalIgnoreCase))
                return new PreviousFieldReferenceNode(fieldName);

            if (name.Equals("auth", StringComparison.OrdinalIgnoreCase))
                return new AuthReferenceNode(fieldName);

            if (name.Equals("record", StringComparison.OrdinalIgnoreCase))
                return new RecordFieldReferenceNode(fieldName);

            // Anything else is a related field reference (e.g., subscription.state)
            return new RelatedFieldReferenceNode(name, fieldName);
        }

        // Bare identifier — field on current record
        return new FieldReferenceNode(name);
    }

    private ExistsNode ParseExists()
    {
        _pos++; // consume EXISTS
        var table = ExpectIdentifier("table name after EXISTS");

        ExpectKeyword("WHERE", "WHERE after EXISTS table");

        var filters = ParseFilterList();
        return new ExistsNode(table, filters);
    }

    private CountNode ParseCount()
    {
        _pos++; // consume COUNT
        var table = ExpectIdentifier("table name after COUNT");

        ExpectKeyword("WHERE", "WHERE after COUNT table");

        var filters = ParseFilterList();

        // After filters, expect a comparison operator and integer
        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Operator)
            throw new ConditionParseException("Expected comparison operator after COUNT ... WHERE filters.");

        var op = _tokens[_pos].Value;
        _pos++;

        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.NumberLiteral)
            throw new ConditionParseException("Expected integer value after COUNT comparison operator.");

        if (!int.TryParse(_tokens[_pos].Value, out var value))
            throw new ConditionParseException($"COUNT comparison value must be an integer, got '{_tokens[_pos].Value}'.");

        _pos++;
        return new CountNode(table, filters, op, value);
    }

    private LookupNode ParseLookup()
    {
        _pos++; // consume LOOKUP

        // Expect table.selectField
        var table = ExpectIdentifier("table name after LOOKUP");
        Expect(TokenType.Dot, "'.' in LOOKUP table.field");
        var selectField = ExpectIdentifier("field name in LOOKUP table.field");

        ExpectKeyword("WHERE", "WHERE after LOOKUP table.field");

        // whereField == whereValue
        var whereField = ExpectIdentifier("WHERE field in LOOKUP");
        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Operator)
            throw new ConditionParseException("Expected comparison operator in LOOKUP WHERE clause.");
        var whereOp = _tokens[_pos].Value;
        _pos++;
        if (whereOp != "==")
            throw new ConditionParseException($"LOOKUP WHERE clause only supports '==', got '{whereOp}'.");

        var whereValue = ParseAtom();

        // Final comparison: op compareValue
        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Operator)
            throw new ConditionParseException("Expected comparison operator after LOOKUP WHERE clause.");
        var op = _tokens[_pos].Value;
        _pos++;

        var compareValue = ParseAtom();

        return new LookupNode(table, selectField, whereField, whereValue, op, compareValue);
    }

    private List<ComparisonNode> ParseFilterList()
    {
        var filters = new List<ComparisonNode>();
        filters.Add(ParseSingleFilter());

        while (_pos < _tokens.Count &&
               _tokens[_pos].Type == TokenType.Keyword &&
               _tokens[_pos].Value.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            // Lookahead: make sure the next tokens form a filter, not a top-level AND
            // A filter is: atom op atom. If after AND we don't see identifier/literal op identifier/literal,
            // we should stop consuming.
            if (!LooksLikeFilter(_pos + 1))
                break;

            _pos++; // consume AND
            filters.Add(ParseSingleFilter());
        }

        return filters;
    }

    private bool LooksLikeFilter(int startPos)
    {
        // A filter starts with an atom (identifier, literal) then an operator then another atom.
        // We need at least 3 tokens.
        if (startPos + 2 >= _tokens.Count) return false;

        var t0 = _tokens[startPos];
        if (t0.Type != TokenType.Identifier && t0.Type != TokenType.StringLiteral &&
            t0.Type != TokenType.NumberLiteral && t0.Type != TokenType.BooleanLiteral)
            return false;

        // Account for dotted identifiers: skip identifier.identifier
        var opIdx = startPos + 1;
        if (opIdx < _tokens.Count - 1 && _tokens[opIdx].Type == TokenType.Dot)
            opIdx += 2; // skip dot and next identifier

        if (opIdx >= _tokens.Count) return false;
        return _tokens[opIdx].Type == TokenType.Operator;
    }

    private ComparisonNode ParseSingleFilter()
    {
        var left = ParseAtom();
        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Operator)
            throw new ConditionParseException("Expected comparison operator in filter.");
        var op = _tokens[_pos].Value;
        _pos++;
        var right = ParseAtom();
        return new ComparisonNode(left, op, right);
    }

    // --- Helpers ---

    private bool MatchKeyword(string keyword)
    {
        if (_pos < _tokens.Count &&
            _tokens[_pos].Type == TokenType.Keyword &&
            _tokens[_pos].Value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            _pos++;
            return true;
        }
        return false;
    }

    private void ExpectKeyword(string keyword, string context)
    {
        if (_pos >= _tokens.Count ||
            _tokens[_pos].Type != TokenType.Keyword ||
            !_tokens[_pos].Value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            var found = _pos < _tokens.Count ? $"'{_tokens[_pos].Value}'" : "end of input";
            throw new ConditionParseException($"Expected {context}, got {found}.");
        }
        _pos++;
    }

    private string ExpectIdentifier(string context)
    {
        if (_pos >= _tokens.Count || _tokens[_pos].Type != TokenType.Identifier)
        {
            var found = _pos < _tokens.Count ? $"'{_tokens[_pos].Value}'" : "end of input";
            throw new ConditionParseException($"Expected {context}, got {found}.");
        }
        var val = _tokens[_pos].Value;
        _pos++;
        return val;
    }

    private void Expect(TokenType type, string context)
    {
        if (_pos >= _tokens.Count || _tokens[_pos].Type != type)
        {
            var found = _pos < _tokens.Count ? $"'{_tokens[_pos].Value}'" : "end of input";
            throw new ConditionParseException($"Expected {context}, got {found}.");
        }
        _pos++;
    }

    // --- Tokenizer ---

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "EXISTS", "COUNT", "LOOKUP", "WHERE"
    };

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < input.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            var startPos = i;

            // String literal: 'value'
            if (input[i] == '\'')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < input.Length && input[i] != '\'')
                {
                    sb.Append(input[i]);
                    i++;
                }
                if (i >= input.Length)
                    throw new ConditionParseException($"Unterminated string literal at position {startPos}.");
                i++; // consume closing quote
                tokens.Add(new Token(TokenType.StringLiteral, sb.ToString(), startPos));
                continue;
            }

            // Operators: ==, !=, >=, <=, >, <
            if (i + 1 < input.Length && input[i..].StartsWith("=="))
            {
                tokens.Add(new Token(TokenType.Operator, "==", startPos));
                i += 2;
                continue;
            }
            if (i + 1 < input.Length && input[i..].StartsWith("!="))
            {
                tokens.Add(new Token(TokenType.Operator, "!=", startPos));
                i += 2;
                continue;
            }
            if (i + 1 < input.Length && input[i..].StartsWith(">="))
            {
                tokens.Add(new Token(TokenType.Operator, ">=", startPos));
                i += 2;
                continue;
            }
            if (i + 1 < input.Length && input[i..].StartsWith("<="))
            {
                tokens.Add(new Token(TokenType.Operator, "<=", startPos));
                i += 2;
                continue;
            }
            if (input[i] == '>')
            {
                tokens.Add(new Token(TokenType.Operator, ">", startPos));
                i++;
                continue;
            }
            if (input[i] == '<')
            {
                tokens.Add(new Token(TokenType.Operator, "<", startPos));
                i++;
                continue;
            }

            // Parentheses
            if (input[i] == '(')
            {
                tokens.Add(new Token(TokenType.LeftParen, "(", startPos));
                i++;
                continue;
            }
            if (input[i] == ')')
            {
                tokens.Add(new Token(TokenType.RightParen, ")", startPos));
                i++;
                continue;
            }

            // Dot
            if (input[i] == '.')
            {
                tokens.Add(new Token(TokenType.Dot, ".", startPos));
                i++;
                continue;
            }

            // Number: optional negative, digits, optional decimal
            if (char.IsDigit(input[i]) || (input[i] == '-' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
            {
                var numStart = i;
                if (input[i] == '-') i++;
                while (i < input.Length && char.IsDigit(input[i])) i++;
                if (i < input.Length && input[i] == '.' && i + 1 < input.Length && char.IsDigit(input[i + 1]))
                {
                    i++; // consume dot
                    while (i < input.Length && char.IsDigit(input[i])) i++;
                }
                tokens.Add(new Token(TokenType.NumberLiteral, input[numStart..i], startPos));
                continue;
            }

            // Identifier or keyword
            if (char.IsLetter(input[i]) || input[i] == '_')
            {
                var idStart = i;
                while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_'))
                    i++;

                var word = input[idStart..i];

                if (word.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    word.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new Token(TokenType.BooleanLiteral, word, startPos));
                }
                else if (Keywords.Contains(word))
                {
                    tokens.Add(new Token(TokenType.Keyword, word, startPos));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Identifier, word, startPos));
                }
                continue;
            }

            throw new ConditionParseException($"Unexpected character '{input[i]}' at position {i}.");
        }

        return tokens;
    }

    // --- Token types ---

    private enum TokenType
    {
        Identifier,
        StringLiteral,
        NumberLiteral,
        BooleanLiteral,
        Operator,
        Keyword,
        LeftParen,
        RightParen,
        Dot
    }

    private readonly record struct Token(TokenType Type, string Value, int Position);
}

/// <summary>
/// Thrown when a condition string cannot be parsed.
/// </summary>
public sealed class ConditionParseException : Exception
{
    public ConditionParseException(string message) : base(message) { }
}
