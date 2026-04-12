"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "../../lib/auth-context";
import { login } from "../../services/api";
import AuthForm from "../../components/AuthForm";

export default function LoginPage() {
  const { setAuth } = useAuth();
  const router = useRouter();

  const handleLogin = async (email: string, password: string) => {
    const data = await login(email, password);
    setAuth(data.token, data.userId, data.role);
    router.push("/dashboard");
  };

  return <AuthForm mode="login" onSubmit={handleLogin} />;
}
