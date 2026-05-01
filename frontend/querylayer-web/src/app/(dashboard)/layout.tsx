"use client";

import { useAuth } from "../../lib/auth-context";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import Sidebar from "../../components/Sidebar";
import Navbar from "../../components/Navbar";

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { token, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !token) {
      router.push("/login");
    }
  }, [isLoading, token, router]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen" style={{ background: 'var(--bg-deep)' }}>
        <div className="flex flex-col items-center gap-4">
          <div
            className="animate-spin w-8 h-8 rounded-full"
            style={{ border: '2px solid var(--glow-violet)', borderTopColor: 'var(--accent)' }}
          />
          <span className="text-sm" style={{ color: 'var(--text-faint)' }}>Loading...</span>
        </div>
      </div>
    );
  }

  if (!token) return null;

  return (
    <div className="flex min-h-screen" style={{ background: 'var(--bg-deep)' }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Navbar />
        <main className="flex-1 p-8 fade-in">
          {children}
        </main>
      </div>
    </div>
  );
}
