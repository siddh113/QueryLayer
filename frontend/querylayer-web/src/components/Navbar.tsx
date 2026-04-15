"use client";

import { useAuth } from "../lib/auth-context";
import { useRouter } from "next/navigation";

export default function Navbar() {
  const { userId, role, logout } = useAuth();
  const router = useRouter();

  const handleLogout = () => {
    logout();
    router.push("/login");
  };

  return (
    <header
      className="h-12 flex items-center justify-between px-5"
      style={{ background: "#ffffff", borderBottom: "1px solid #f0efee" }}
    >
      <div style={{ width: "1px" }} />
      <div className="flex items-center gap-3">
        {userId && (
          <>
            <span
              className="text-xs px-2 py-0.5 rounded-full font-medium"
              style={{
                background: "#ede9fe",
                color: "#6d28d9",
                fontSize: "11px",
              }}
            >
              {role}
            </span>
            <button
              onClick={handleLogout}
              className="text-xs transition-colors"
              style={{ color: "#a8a29e" }}
              onMouseEnter={(e) => { e.currentTarget.style.color = "#1c1917"; }}
              onMouseLeave={(e) => { e.currentTarget.style.color = "#a8a29e"; }}
            >
              Sign out
            </button>
          </>
        )}
      </div>
    </header>
  );
}
