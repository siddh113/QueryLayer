"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../lib/auth-context";

export default function HomePage() {
  const { token, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading) {
      router.replace(token ? "/dashboard" : "/login");
    }
  }, [isLoading, token, router]);

  return (
    <div className="flex items-center justify-center min-h-screen" style={{ background: 'var(--bg-deep)' }}>
      <div className="flex flex-col items-center gap-4">
        <div className="relative">
          <div className="w-10 h-10 rounded-xl border-2 border-transparent animate-spin"
            style={{
              borderImage: 'linear-gradient(135deg, #6366f1, #06b6d4) 1',
              borderRadius: '12px',
            }}
          />
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="w-3 h-3 rounded-full" style={{ background: 'linear-gradient(135deg, #6366f1, #06b6d4)' }} />
          </div>
        </div>
        <span className="text-sm tracking-wide" style={{ color: 'var(--text-muted)' }}>Loading...</span>
      </div>
    </div>
  );
}
