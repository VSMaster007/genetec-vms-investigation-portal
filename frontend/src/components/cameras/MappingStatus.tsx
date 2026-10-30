export function MappingStatus({ mapped, mock }: { mapped: boolean; mock: boolean }) {
  if (!mapped) {
    return <span className="text-amber-300">Unmapped</span>
  }
  return <span className="text-emerald-300">{mock ? 'Mapped · mock' : 'Mapped'}</span>
}
