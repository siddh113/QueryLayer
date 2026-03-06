"use client";

import { useEffect, useState } from "react";
import { api } from "../services/api";

export default function HomePage() {
  const [status, setStatus] = useState("Checking backend...");

  useEffect(() => {
    api.get("/api/health")
      .then(() => setStatus("Backend connected"))
      .catch(() => setStatus("Backend unavailable"));
  }, []);

  return (
    <main className="p-8">
      <h1 className="text-3xl font-bold mb-4">QueryLayer</h1>
      <p>{status}</p>
    </main>
  );
}