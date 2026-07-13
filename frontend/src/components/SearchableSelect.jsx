import { useState, useRef, useEffect, useCallback } from 'react'

export default function SearchableSelect({ value, options, onChange, placeholder = 'Select...', searchPlaceholder = 'Search...', clearLabel = 'Clear', disabled = false }) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const [highlightIdx, setHighlightIdx] = useState(-1)
  const containerRef = useRef(null)
  const searchRef = useRef(null)

  const filtered = options.filter(o => o.toLowerCase().includes(search.toLowerCase()))

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

  const handleKeyDown = useCallback(e => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setHighlightIdx(i => Math.min(i + 1, filtered.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setHighlightIdx(i => Math.max(i - 1, -1)) }
    else if (e.key === 'Enter' && highlightIdx >= 0 && highlightIdx < filtered.length) {
      e.preventDefault()
      onChange(filtered[highlightIdx])
      setOpen(false)
    }
    else if (e.key === 'Escape') { e.preventDefault(); setOpen(false) }
  }, [filtered, highlightIdx, onChange])

  return (
    <div className="ssel" ref={containerRef} style={disabled ? { opacity: 0.6, cursor: 'not-allowed' } : undefined}>
      <button type="button" className="ssel-trigger" onClick={() => { if (!disabled) setOpen(o => !o) }}>
        <span className={`ssel-value${!value ? ' ssel-placeholder' : ''}`}>{value || placeholder}</span>
        <i className={`fas fa-chevron-${open ? 'up' : 'down'} ssel-arrow`} />
      </button>
      {open && (
        <div className="ssel-dropdown">
          <div className="ssel-search">
            <i className="fas fa-search ssel-search-icon" />
            <input ref={searchRef} type="text" placeholder={searchPlaceholder} value={search} onChange={e => { setSearch(e.target.value); setHighlightIdx(-1) }} onKeyDown={handleKeyDown} />
            {search && <button className="ssel-search-clear" onClick={() => setSearch('')}><i className="fas fa-times" /></button>}
          </div>
          <div className="ssel-options">
            <button type="button" className={`ssel-opt ssel-clear`} onClick={() => { onChange(''); setOpen(false) }}>
              <i className="fas fa-times-circle" /> {clearLabel}
            </button>
            {filtered.length === 0 ? (
              <div className="ssel-empty">No applications found</div>
            ) : (
              filtered.map((opt, i) => (
                <button
                  key={opt}
                  type="button"
                  className={`ssel-opt${value === opt ? ' ssel-selected' : ''}${highlightIdx === i ? ' ssel-highlight' : ''}`}
                  onClick={() => { onChange(opt); setOpen(false) }}
                  onMouseEnter={() => setHighlightIdx(i)}
                >
                  {value === opt && <i className="fas fa-check ssel-check" />}
                  {opt}
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}
