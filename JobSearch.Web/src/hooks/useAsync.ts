import { useCallback, useEffect, useState, type DependencyList } from "react";
import { InsufficientCreditsError } from "../api";

function errorMessage(e: unknown): string {
  if (e instanceof InsufficientCreditsError) return "You're out of credits.";
  return e instanceof Error ? e.message : "Something went wrong";
}

interface AsyncDataState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

// Loads automatically on mount and whenever deps change (the GET/list-page pattern).
// Call the returned reload() for a manual refresh without changing any dependency.
export function useAsyncData<T>(fn: () => Promise<T>, deps: DependencyList) {
  const [state, setState] = useState<AsyncDataState<T>>({ data: null, loading: true, error: null });
  const [tick, setTick] = useState(0);

  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true, error: null }));
    fn()
      .then(data => setState({ data, loading: false, error: null }))
      .catch(e => setState({ data: null, loading: false, error: errorMessage(e) }));
    // ponytail: react-hooks/use-memo wants this array to be an inline literal, but this hook's
    // whole point is running fn() again when the *caller's* deps change — deps is a pass-through
    // parameter by design (every call site has a different fetch function and a different deps
    // shape), so it can never be written as a literal here. No upgrade path: this isn't a
    // shortcut waiting on a follow-up, it's a permanent, deliberate exception to the rule for a
    // hook whose contract is "you bring your own deps array" (see useAsyncData's own doc comment
    // above). exhaustive-deps is also off for the same reason — `fn`'s identity isn't meant to
    // be tracked, only `deps` is.
    // eslint-disable-next-line react-hooks/use-memo, react-hooks/exhaustive-deps
  }, deps);

  useEffect(() => { load(); }, [load, tick]);

  return { ...state, reload: () => setTick(t => t + 1) };
}

// For a call triggered by a user action (submit/click) rather than loaded automatically.
export function useAsyncAction<Args extends unknown[], T>(fn: (...args: Args) => Promise<T>) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const execute = useCallback(async (...args: Args): Promise<T> => {
    setLoading(true);
    setError(null);
    try {
      const result = await fn(...args);
      setLoading(false);
      return result;
    } catch (e) {
      setLoading(false);
      setError(errorMessage(e));
      throw e;
    }
  }, [fn]);

  return { execute, loading, error };
}
