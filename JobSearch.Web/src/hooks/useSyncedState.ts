import { useEffect, useState, type Dispatch, type SetStateAction } from "react";

// Seeds local, user-editable state from `source` once it arrives (the common "hydrate a draft
// from async profile/query data, then let the user freely edit it" idiom repeated across the
// criteria, resume, settings and sources pages). Re-seeds whenever `source`'s identity changes,
// same as the useEffect+setState it replaces — it does not protect edits made in between two
// source changes, because none of its call sites needed that.
export function useSyncedState<S, T>(
  source: S | null | undefined,
  initial: T,
  map: (source: S) => T,
): [T, Dispatch<SetStateAction<T>>] {
  const [value, setValue] = useState<T>(initial);

  useEffect(() => {
    // ponytail: react-hooks/set-state-in-effect exists to catch derived-state-in-effect
    // (a value computable from props/state during render, needlessly stashed via an effect).
    // This hook is the opposite case the rule's own docs call out as legitimate: syncing local
    // state to an external system (async-loaded data) on the *first* time it becomes available,
    // then leaving it alone for the user to edit. That can't be done during render — `source`
    // arriving is itself the event this needs to react to. This is the one place that pattern is
    // now allowed to exist at all; the six inline occurrences it replaced are gone.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (source != null) setValue(map(source));
    // `map` is expected to be a fresh inline function per render (same contract as
    // useAsyncData's `deps` param) — depending on `source` alone is the point, so this only
    // re-seeds when the data itself changes, never because the caller's mapper got a new identity.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [source]);

  return [value, setValue];
}
