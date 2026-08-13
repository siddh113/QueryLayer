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

  return (
    <div className="min-h-screen flex items-center justify-center relative overflow-hidden noise-bg"
      style={{ background: 'var(--bg-deep)' }}
    >
      {/* Background glow effects */}
      <div className="absolute top-1/4 left-1/3 w-96 h-96 rounded-full"
        style={{ background: 'radial-gradient(circle, rgba(255, 107, 74, 0.14), transparent 70%)', filter: 'blur(60px)' }}
      />
      <div className="absolute bottom-1/4 right-1/3 w-80 h-80 rounded-full"
        style={{ background: 'radial-gradient(circle, rgba(109, 93, 252, 0.1), transparent 70%)', filter: 'blur(60px)' }}
      />

      <div className="w-full max-w-sm fade-in relative z-10">
        {/* Logo */}
        <div className="text-center mb-10">
          <div className="flex items-center justify-center gap-3 mb-4">
            <div className="w-11 h-11 rounded-xl flex items-center justify-center text-lg font-bold"
              style={{ background: 'linear-gradient(135deg, #FF6B4A, #6D5DFC)', boxShadow: '0 0 24px rgba(255, 107, 74, 0.25)' }}
            >
              <span className="text-white">Q</span>
            </div>
          </div>
          <h1 className="text-xl font-bold gradient-text-hero mb-2">QueryLayer</h1>
          <p className="text-sm" style={{ color: 'var(--text-muted)' }}>
            {mode === "login" ? "Welcome back. Sign in to continue." : "Create your account to get started."}
          </p>
        </div>

        {/* Card */}
        <div className="rounded-2xl p-7 space-y-5 glass-card"
          style={{ boxShadow: '0 0 60px var(--glow-cyan), 0 0 30px var(--glow-violet)' }}
        >
          {error && (
            <div className="text-sm px-4 py-3 rounded-xl"
              style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error-text)' }}
            >
              {error}
            </div>
          )}

          <div>
            <label className="block text-[13px] font-medium mb-2" style={{ color: 'var(--text-muted)' }}>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              placeholder="you@example.com"
              className="w-full rounded-xl px-4 py-2.5 text-sm input-glow focus:outline-none"
              style={{
                background: 'var(--input-bg)',
                border: '1px solid var(--input-border)',
                color: 'var(--text-primary)',
              }}
            />
          </div>

          <div>
            <label className="block text-[13px] font-medium mb-2" style={{ color: 'var(--text-muted)' }}>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              placeholder="Enter your password"
              className="w-full rounded-xl px-4 py-2.5 text-sm input-glow focus:outline-none"
              style={{
                background: 'var(--input-bg)',
                border: '1px solid var(--input-border)',
                color: 'var(--text-primary)',
              }}
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-xl text-sm font-semibold btn-gradient disabled:opacity-50 disabled:cursor-not-allowed"
            onClick={handleSubmit}
          >
            <span className="flex items-center justify-center gap-2">
              {loading ? (
                <>
                  <span className="animate-spin w-4 h-4 rounded-full"
                    style={{ border: '2px solid currentColor', borderTopColor: 'transparent', opacity: 0.85 }}
                  />
                  Please wait...
                </>
              ) : (
                mode === "login" ? "Sign in" : "Create account"
              )}
            </span>
          </button>

          <p className="text-center text-[13px]" style={{ color: 'var(--text-muted)' }}>
            {mode === "login" ? (
              <>
                No account?{" "}
                <Link href="/signup" className="font-medium transition-colors duration-200"
                  style={{ color: 'var(--accent-cyan)' }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = 'var(--accent-cyan-light)'; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = 'var(--accent-cyan)'; }}
                >
                  Sign up
                </Link>
              </>
            ) : (
              <>
                Have an account?{" "}
                <Link href="/login" className="font-medium transition-colors duration-200"
                  style={{ color: 'var(--accent-cyan)' }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = 'var(--accent-cyan-light)'; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = 'var(--accent-cyan)'; }}
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
