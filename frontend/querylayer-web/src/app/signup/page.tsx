"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "../../lib/auth-context";
import { signup } from "../../services/api";
import AuthForm from "../../components/AuthForm";

export default function SignupPage() {
  const { setAuth } = useAuth();
  const router = useRouter();

  const handleSignup = async (email: string, password: string) => {
    const data = await signup(email, password);
    setAuth(data.token, data.userId, data.role);
    router.push("/dashboard");
  };

  return <AuthForm mode="signup" onSubmit={handleSignup} />;
}
