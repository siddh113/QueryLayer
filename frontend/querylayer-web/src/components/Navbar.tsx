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
    <header className="h-14 bg-white border-b border-gray-200 flex items-center justify-between px-6">
      <div className="text-sm text-gray-500">
        QueryLayer Dashboard
      </div>
      <div className="flex items-center gap-4">
        {userId && (
          <>
            <span className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded">
              {role}
            </span>
            <button
              onClick={handleLogout}
              className="text-sm text-gray-600 hover:text-gray-900 transition-colors"
            >
              Logout
            </button>
          </>
        )}
      </div>
    </header>
  );
}
