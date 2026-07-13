import { useState, useCallback, useMemo, useRef, useEffect } from 'react'
import SearchableSelect from './SearchableSelect'

export default function Filters({ tickets, filters, onFilterChange, onExportAll, onExportSelected, selectedCount }) {
  const [exportOpen, setExportOpen] = useState(false)
  const exportRef = useRef(null)

  const apps = useMemo(() => [...new Set(tickets.map(t => t.application))].sort(), [tickets])

  const handleChange = useCallback((key, value) => {
    onFilterChange({ ...filters, [key]: value })
  }, [filters, onFilterChange])

  useEffect(() => {
    const handler = e => {
      if (exportRef.current && !exportRef.current.contains(e.target)) setExportOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  return (
    <div className="filters">
      <div className="filter-item search-box">
        <i className="fas fa-search" />
        <input
          type="text"
          placeholder="Search tickets by ID, subject, or person..."
          value={filters.search}
          onChange={e => handleChange('search', e.target.value)}
        />
      </div>
      <div className="filter-item">
        <label>Status</label>
        <select value={filters.status} onChange={e => handleChange('status', e.target.value)}>
          <option value="all">All Statuses</option>
          <option value="open">Open</option>
          <option value="in_progress">In Progress</option>
          <option value="waiting">Waiting</option>
          <option value="resolved">Resolved</option>
          <option value="closed">Closed</option>
        </select>
      </div>
      <div className="filter-item">
        <label>Application</label>
        <SearchableSelect
          value={filters.app === 'all' ? '' : filters.app}
          options={apps}
          onChange={val => handleChange('app', val || 'all')}
          placeholder="All Applications"
          searchPlaceholder="Search Application..."
          clearLabel="All Applications"
        />
      </div>
      <div className="filter-item">
        <label>From</label>
        <input type="date" value={filters.from} onChange={e => handleChange('from', e.target.value)} />
      </div>
      <div className="filter-item">
        <label>To</label>
        <input type="date" value={filters.to} onChange={e => handleChange('to', e.target.value)} />
      </div>
      <div className="filter-export" ref={exportRef} style={{ position: 'relative' }}>
        <button className="btn btn-outline" onClick={() => setExportOpen(o => !o)}>
          <i className="fas fa-download" /> Export <i className={`fas fa-chevron-${exportOpen ? 'up' : 'down'}`} style={{ fontSize: '.65rem', marginLeft: 2 }} />
        </button>
        {exportOpen && (
          <div className="dropdown-menu show" style={{ position: 'absolute', right: 0, top: '100%', marginTop: 4, zIndex: 50 }}>
            <a href="#" onClick={e => { e.preventDefault(); setExportOpen(false); onExportAll?.() }}>Export All Tickets</a>
            <a href="#" onClick={e => { if (!selectedCount) return; e.preventDefault(); setExportOpen(false); onExportSelected?.() }} style={selectedCount === 0 ? { color: 'var(--text-muted)', cursor: 'default', pointerEvents: 'none' } : {}}>Export Selected Tickets</a>
          </div>
        )}
      </div>
    </div>
  )
}
