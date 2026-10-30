export function StatusPill({ state }: { state: string }) {
  const tone = state === 'Healthy' ? 'bg-emerald-500' : state === 'Mock' ? 'bg-amber-400' : state === 'NotConfigured' ? 'bg-slate-400' : 'bg-rose-500'
  return (
    <span className="inline-flex items-center gap-2 text-sm">
      <span className={`h-2.5 w-2.5 rounded-full ${tone}`} />
      {state}
    </span>
  )
}
