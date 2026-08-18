import { useLoginUrl } from "../hooks/useAuth";

export function LandingPage() {
  const loginUrl = useLoginUrl();

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-6">
      <div className="w-full max-w-md rounded-2xl border border-gray-200 bg-white p-8 text-center shadow-sm">
        <h1 className="text-xl font-semibold text-gray-800">Work Santa</h1>
        <p className="mt-2 text-sm text-gray-500">
          Evaluate job postings against your own criteria, and generate tailored CVs, cover
          letters, and application answers grounded in your real background.
        </p>
        <a
          href={loginUrl}
          className="mt-6 inline-block w-full rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-blue-700"
        >
          Sign in with Google
        </a>
      </div>
    </div>
  );
}
