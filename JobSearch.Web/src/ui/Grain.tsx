// A fixed film of monochrome noise over the whole viewport. It is what stops the large flat
// Slate areas reading as untextured digital grey.
//
// `fixed` is load bearing. Attached to a scrolling container the noise would travel with the
// content and read as a rendering fault; fixed, it behaves like the grain of the paper the app is
// printed on. Never move this inside a scroll container.
//
// pointer-events-none because it covers everything, including every button underneath it.
export function Grain({ className = "" }: { className?: string }) {
  return <div aria-hidden="true" className={`grain-overlay pointer-events-none fixed inset-0 z-40 opacity-50 ${className}`} />;
}
