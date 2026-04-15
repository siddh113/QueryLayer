"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";

interface AuthFormProps {
  mode: "login" | "signup";
  onSubmit: (email: string, password: string) => Promise<void>;
}

export default function AuthForm({ mode, onSubmit }: AuthFormProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await onSubmit(email, password);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string } } }).response?.data?.error
          : undefined;
      setError(msg || "Authentication failed");
    } finally {
      setLoading(false);
    }
  };

  const inputStyle: React.CSSProperties = {
    width: "100%",
    background: "#0f0f0f",
    border: "1px solid #262626",
    borderRadius: "6px",
    padding: "8px 12px",
    fontSize: "14px",
    color: "#ededed",
    outline: "none",
    transition: "border-color 0.15s",
  };

  return (
    <div
      className="min-h-screen flex items-center justify-center"
      style={{ background: "#0f0f0f" }}
    >
      <div className="w-full max-w-sm fade-in">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="flex items-center justify-center gap-2.5 mb-3">
            <div
              className="w-9 h-9 rounded-lg flex items-center justify-center text-base font-bold"
              style={{ background: "#3ecf8e", color: "#0f0f0f" }}
            >
              Q
            </div>
            <span className="text-lg font-semibold" style={{ color: "#ededed" }}>QueryLayer</span>
          </div>
          <p className="text-sm" style={{ color: "#525252" }}>
            {mode === "login" ? "Sign in to your account" : "Create a new account"}
          </p>
        </div>

        {/* Card */}
        <div
          className="rounded-xl p-6 space-y-4"
          style={{ background: "#141414", border: "1px solid #262626" }}
        >
          {error && (
            <div
              className="text-sm px-3 py-2 rounded-md"
              style={{
                background: "rgba(248,113,113,0.06)",
                border: "1px solid rgba(248,113,113,0.15)",
                color: "#f87171",
              }}
            >
              {error}
            </div>
          )}

          <div>
            <label className="block text-sm mb-1.5" style={{ color: "#a1a1a1" }}>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              placeholder="you@example.com"
              style={inputStyle}
              onFocus={(e) => { e.target.style.borderColor = "#3ecf8e"; }}
              onBlur={(e) => { e.target.style.borderColor = "#262626"; }}
            />
          </div>

          <div>
            <label className="block text-sm mb-1.5" style={{ color: "#a1a1a1" }}>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              placeholder="••••••••"
              style={inputStyle}
              onFocus={(e) => { e.target.style.borderColor = "#3ecf8e"; }}
              onBlur={(e) => { e.target.style.borderColor = "#262626"; }}
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2 rounded-md text-sm font-medium transition-all"
            style={{
              background: loading ? "#1f1f1f" : "#3ecf8e",
              color: loading ? "#525252" : "#0f0f0f",
              cursor: loading ? "not-allowed" : "pointer",
              border: "none",
            }}
            onMouseEnter={(e) => { if (!loading) e.currentTarget.style.background = "#5de0a3"; }}
            onMouseLeave={(e) => { if (!loading) e.currentTarget.style.background = "#3ecf8e"; }}
            onClick={handleSubmit}
          >
            {loading ? (
              <span className="flex items-center justify-center gap-2">
                <span className="animate-spin w-3.5 h-3.5 border border-current border-t-transparent rounded-full" />
                Please wait...
              </span>
            ) : (
              mode === "login" ? "Sign in" : "Create account"
            )}
          </button>

          <p className="text-center text-sm" style={{ color: "#525252" }}>
            {mode === "login" ? (
              <>
                No account?{" "}
                <Link href="/signup" style={{ color: "#a1a1a1" }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = "#ededed"; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = "#a1a1a1"; }}
                >
                  Sign up
                </Link>
              </>
            ) : (
              <>
                Have an account?{" "}
                <Link href="/login" style={{ color: "#a1a1a1" }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = "#ededed"; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = "#a1a1a1"; }}
                >
                  Sign in
                </Link>
              </>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}
