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
      <div className="flex items-center justify-center min-h-screen" style={{ background: "#f5f5f4" }}>
        <div className="flex items-center gap-2 text-sm" style={{ color: "#a8a29e" }}>
          <span className="animate-spin w-4 h-4 border border-current border-t-transparent rounded-full inline-block" />
          Loading...
        </div>
      </div>
    );
  }

  if (!token) return null;

  return (
    <div className="flex min-h-screen" style={{ background: "#f5f5f4" }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Navbar />
        <main className="flex-1 p-6 fade-in">{children}</main>
      </div>
    </div>
  );
}
