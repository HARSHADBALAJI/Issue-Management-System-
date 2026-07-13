import { useState, useRef, useEffect, useMemo } from 'react'

export default function SpocSelector({ users, currentAssignedId, assignedName, disabled, loading, onAssign, onClear, placeholder = 'Search by name or email' }) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const [highlightIdx, setHighlightIdx] = useState(-1)
  const containerRef = useRef(null)
  const searchRef = useRef(null)
  const optionsRef = useRef(null)

  const filtered = useMemo(() => {
    if (!users) return []
    const q = search.toLowerCase().trim()
    if (!q) return users
    return users.filter(u =>
      (u.name || '').toLowerCase().includes(q) ||
      (u.email || '').toLowerCase().includes(q)
    )
  }, [users, search])

  useEffect(() => {
    if (!open) { setSearch(''); setHighlightIdx(-1) }
    else { setTimeout(() => searchRef.current?.focus(), 50) }
  }, [open])

  useEffect(() => {
    const handler = e => {
      if (containerRef.current && !containerRef.current.contains(e.target)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const handleKeyDown = e => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setHighlightIdx(i => Math.min(i + 1, filtered.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setHighlightIdx(i => Math.max(i - 1, -1)) }
    else if (e.key === 'Enter' && highlightIdx >= 0 && highlightIdx < filtered.length) {
      e.preventDefault()
      const selected = filtered[highlightIdx]
      if (selected.id === currentAssignedId) { onClear?.(); setOpen(false); return }
      onAssign(selected.id)
      setOpen(false)
    }
    else if (e.key === 'Escape') { e.preventDefault(); setOpen(false) }
  }

  useEffect(() => {
    if (highlightIdx >= 0 && optionsRef.current) {
      const items = optionsRef.current.querySelectorAll('.spoc-sel-opt')
      if (items[highlightIdx]) items[highlightIdx].scrollIntoView({ block: 'nearest' })
    }
  }, [highlightIdx])

  return (
    <div className="spoc-sel" ref={containerRef} onClick={e => e.stopPropagation()}>
      <button type="button" className="spoc-sel-trigger" onClick={() => { if (!disabled) setOpen(o => !o) }} disabled={disabled}>
        <span className="spoc-sel-value">
          {assignedName ? (
            <><i className="fas fa-user-check" style={{ color: 'var(--primary)', marginRight: 6, fontSize: '.78rem' }} />{assignedName}</>
          ) : (
            <><i className="fas fa-user-plus" style={{ color: 'var(--text-muted)', marginRight: 6, fontSize: '.78rem' }} />Unassigned</>
          )}
        </span>
        {!disabled && <i className={`fas fa-chevron-${open ? 'up' : 'down'} spoc-sel-arrow`} />}
      </button>

      {open && (
        <div className="spoc-sel-dropdown">
          <div className="spoc-sel-search">
            <i className="fas fa-search spoc-sel-search-icon" />
            <input ref={searchRef} type="text" placeholder={placeholder} value={search} onChange={e => { setSearch(e.target.value); setHighlightIdx(-1) }} onKeyDown={handleKeyDown} />
            {search && <button className="spoc-sel-search-clear" onClick={() => setSearch('')}><i className="fas fa-times" /></button>}
          </div>

          {loading ? (
            <div className="spoc-sel-loading"><i className="fas fa-spinner fa-spin" /> Loading SPOCs...</div>
          ) : (
            <div className="spoc-sel-options" ref={optionsRef}>
              {currentAssignedId != null && (
                <button type="button" className="spoc-sel-opt spoc-sel-clear" onClick={() => { setOpen(false); onClear?.() }}>
                  <i className="fas fa-times-circle" style={{ color: 'var(--text-muted)', fontSize: '.72rem' }} />
                  <span style={{ color: 'var(--text-muted)' }}>Clear Assignment</span>
                </button>
              )}

              {filtered.length === 0 ? (
                <div className="spoc-sel-empty">No SPOCs found</div>
              ) : (
                filtered.map((u, i) => {
                  const isAssigned = u.id === currentAssignedId
                  return (
                    <button key={u.id} type="button"
                      className={`spoc-sel-opt${isAssigned ? ' spoc-sel-current' : ''}${highlightIdx === i ? ' spoc-sel-highlight' : ''}`}
                      onClick={() => { if (isAssigned) { onClear?.(); setOpen(false); return }; onAssign(u.id); setOpen(false) }}
                      onMouseEnter={() => setHighlightIdx(i)}>
                      {isAssigned ? (
                        <i className="fas fa-check-circle spoc-sel-check" style={{ color: 'var(--primary)', fontSize: '.82rem' }} />
                      ) : (
                        <i className="fas fa-user-circle spoc-sel-check" style={{ color: 'var(--text-muted)', fontSize: '.82rem' }} />
                      )}
                      <span className="spoc-sel-opt-name">{u.name}</span>
                      <span className="spoc-sel-opt-email">{u.email}</span>
                    </button>
                  )
                })
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
