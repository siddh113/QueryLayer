"use client";

import { useAuth } from "../../../lib/auth-context";

export default function SettingsPage() {
  const { userId, role } = useAuth();

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Settings</h1>
      <div className="bg-white rounded-lg border border-gray-200 p-5 max-w-lg">
        <h2 className="font-semibold text-gray-900 mb-4">Account Info</h2>
        <dl className="space-y-3 text-sm">
          <div>
            <dt className="text-gray-500">User ID</dt>
            <dd className="font-mono text-gray-900">{userId || "-"}</dd>
          </div>
          <div>
            <dt className="text-gray-500">Role</dt>
            <dd className="text-gray-900">{role || "-"}</dd>
          </div>
        </dl>
      </div>
    </div>
  );
}
