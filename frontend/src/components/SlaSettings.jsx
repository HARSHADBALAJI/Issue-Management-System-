import { useState, useEffect } from 'react'
import { slaService } from '../services/slaService'

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const PRIORITIES = ['critical', 'high', 'medium', 'low']

export default function SlaSettings() {
  const [tab, setTab] = useState('hours')
  const [settings, setSettings] = useState({ workStartTime: '09:00', workEndTime: '17:40', notifyBeforeHours: 24 })
  const [policies, setPolicies] = useState([])
  const [holidays, setHolidays] = useState([])
  const [weeklyRules, setWeeklyRules] = useState([])
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState(null)
  const [newHoliday, setNewHoliday] = useState({ date: '', name: '' })
  const [editHoliday, setEditHoliday] = useState(null)
  const [newRule, setNewRule] = useState({ dayOfWeek: 'Saturday', weekType: 'EverySecondAndFourth', description: '' })

  useEffect(() => {
    slaService.getSettings().then(setSettings).catch(() => {})
    slaService.getPolicies().then(setPolicies).catch(() => {})
    slaService.getHolidays().then(setHolidays).catch(() => {})
    slaService.getWeeklyRules().then(setWeeklyRules).catch(() => {})
  }, [])

  function flash(msg, type = 'success') {
    setMsg({ text: msg, type })
    setTimeout(() => setMsg(null), 3000)
  }

  async function saveSettings() {
    setSaving(true)
    try {
      await slaService.updateSettings(settings)
      flash('Settings saved')
    } catch { flash('Failed to save settings', 'error') }
    setSaving(false)
  }

  async function updatePolicy(p) {
    try {
      await slaService.updatePolicy(p.id, { durationDays: p.durationDays })
      flash(`${p.priority} policy updated`)
    } catch { flash('Failed to update policy', 'error') }
  }

  async function addHoliday() {
    if (!newHoliday.date || !newHoliday.name) return
    try {
      await slaService.createHoliday(newHoliday)
      const list = await slaService.getHolidays()
      setHolidays(list)
      setNewHoliday({ date: '', name: '' })
      flash('Holiday added')
    } catch { flash('Failed to add holiday', 'error') }
  }

  async function saveEditHoliday() {
    if (!editHoliday) return
    try {
      await slaService.updateHoliday(editHoliday.id, { date: editHoliday.date, name: editHoliday.name })
      const list = await slaService.getHolidays()
      setHolidays(list)
      setEditHoliday(null)
      flash('Holiday updated')
    } catch { flash('Failed to update holiday', 'error') }
  }

  async function deleteHoliday(id) {
    if (!confirm('Delete this holiday?')) return
    try {
      await slaService.deleteHoliday(id)
      setHolidays(prev => prev.filter(h => h.id !== id))
      flash('Holiday deleted')
    } catch { flash('Failed to delete holiday', 'error') }
  }

  async function addRule() {
    if (!newRule.dayOfWeek) return
    try {
      await slaService.createWeeklyRule(newRule)
      const list = await slaService.getWeeklyRules()
      setWeeklyRules(list)
      setNewRule({ dayOfWeek: 'Saturday', weekType: 'EverySecondAndFourth', description: '' })
      flash('Rule added')
    } catch { flash('Failed to add rule', 'error') }
  }

  async function deleteRule(id) {
    try {
      await slaService.deleteWeeklyRule(id)
      setWeeklyRules(prev => prev.filter(r => r.id !== id))
      flash('Rule deleted')
    } catch { flash('Failed to delete rule', 'error') }
  }

  const tabs = [
    { key: 'hours', label: 'Working Hours' },
    { key: 'weekly', label: 'Weekly Holidays' },
    { key: 'holidays', label: 'Government Holidays' },
    { key: 'policies', label: 'SLA Policies' },
  ]

  return (
    <div style={{ padding: '24px 32px' }}>
      <div className="page-heading" style={{ marginBottom: 24 }}>
        <h1>SLA Settings</h1>
      </div>

      {msg && (
        <div style={{
          padding: '10px 16px', borderRadius: 6, marginBottom: 16, fontSize: '.85rem',
          background: msg.type === 'error' ? '#FEF2F2' : '#ECFDF5',
          color: msg.type === 'error' ? '#DC2626' : '#065F46',
          border: `1px solid ${msg.type === 'error' ? '#FECACA' : '#A7F3D0'}`
        }}>{msg.text}</div>
      )}

      <div className="tdm-tabs" style={{ marginBottom: 24 }}>
        {tabs.map(t => (
          <button key={t.key} className={`tdm-tab ${tab === t.key ? 'active' : ''}`} onClick={() => setTab(t.key)}>
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'hours' && (
        <div style={{ maxWidth: 500, background: '#fff', border: '1px solid var(--border)', borderRadius: 8, padding: 24 }}>
          <div className="tdm-grid-item" style={{ marginBottom: 16 }}>
            <label>Work Start Time</label>
            <input type="time" value={settings.workStartTime} onChange={e => setSettings({ ...settings, workStartTime: e.target.value })}
              style={{ fontFamily: 'inherit', fontSize: '.9rem', padding: '6px 10px', border: '1px solid var(--border)', borderRadius: 5, maxWidth: 200 }} />
          </div>
          <div className="tdm-grid-item" style={{ marginBottom: 16 }}>
            <label>Work End Time</label>
            <input type="time" value={settings.workEndTime} onChange={e => setSettings({ ...settings, workEndTime: e.target.value })}
              style={{ fontFamily: 'inherit', fontSize: '.9rem', padding: '6px 10px', border: '1px solid var(--border)', borderRadius: 5, maxWidth: 200 }} />
          </div>
          <div className="tdm-grid-item" style={{ marginBottom: 24 }}>
            <label>Notify Before (hours)</label>
            <input type="number" value={settings.notifyBeforeHours} onChange={e => setSettings({ ...settings, notifyBeforeHours: parseInt(e.target.value) || 24 })}
              style={{ fontFamily: 'inherit', fontSize: '.9rem', padding: '6px 10px', border: '1px solid var(--border)', borderRadius: 5, maxWidth: 200 }} />
          </div>
          <button className="btn btn-primary btn-sm" onClick={saveSettings} disabled={saving}>
            {saving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      )}

      {tab === 'weekly' && (
        <div style={{ maxWidth: 600 }}>
          <div style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 8, padding: 24, marginBottom: 16 }}>
            <h3 style={{ margin: '0 0 16px', fontSize: '1rem' }}>Add Weekly Holiday Rule</h3>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <div className="tdm-grid-item">
                <label>Day of Week</label>
                <select value={newRule.dayOfWeek} onChange={e => setNewRule({ ...newRule, dayOfWeek: e.target.value })}
                  style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '5px 8px', border: '1px solid var(--border)', borderRadius: 5 }}>
                  {DAYS.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
              <div className="tdm-grid-item">
                <label>Week Type</label>
                <select value={newRule.weekType} onChange={e => setNewRule({ ...newRule, weekType: e.target.value })}
                  style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '5px 8px', border: '1px solid var(--border)', borderRadius: 5 }}>
                  <option value="All">Every Week</option>
                  <option value="EverySecondAndFourth">2nd & 4th</option>
                </select>
              </div>
              <div className="tdm-grid-item">
                <label>Description</label>
                <input type="text" value={newRule.description} onChange={e => setNewRule({ ...newRule, description: e.target.value })}
                  placeholder="e.g. 2nd and 4th Saturday"
                  style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '5px 8px', border: '1px solid var(--border)', borderRadius: 5, width: 180 }} />
              </div>
              <button className="btn btn-primary btn-sm" onClick={addRule}>Add Rule</button>
            </div>
          </div>

          <div style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '.85rem' }}>
              <thead><tr style={{ background: '#F9FAFB' }}>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Day</th>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Type</th>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Description</th>
                <th style={{ padding: '10px 14px', textAlign: 'right', borderBottom: '1px solid var(--border)' }}>Action</th>
              </tr></thead>
              <tbody>
                {weeklyRules.map(r => (
                  <tr key={r.id}>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>{r.dayOfWeek}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>{r.weekType}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>{r.description}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6', textAlign: 'right' }}>
                      <button className="td-side-act-btn" style={{ color: '#DC2626' }} onClick={() => deleteRule(r.id)}>Delete</button>
                    </td>
                  </tr>
                ))}
                {weeklyRules.length === 0 && (
                  <tr><td colSpan={4} style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)' }}>No weekly holiday rules configured.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'holidays' && (
        <div style={{ maxWidth: 700 }}>
          <div style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 8, padding: 24, marginBottom: 16 }}>
            <h3 style={{ margin: '0 0 16px', fontSize: '1rem' }}>{editHoliday ? 'Edit Holiday' : 'Add Government Holiday'}</h3>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <div className="tdm-grid-item">
                <label>Date</label>
                <input type="date" value={editHoliday ? editHoliday.date : newHoliday.date}
                  onChange={e => {
                    if (editHoliday) setEditHoliday({ ...editHoliday, date: e.target.value })
                    else setNewHoliday({ ...newHoliday, date: e.target.value })
                  }}
                  style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '5px 8px', border: '1px solid var(--border)', borderRadius: 5 }} />
              </div>
              <div className="tdm-grid-item">
                <label>Holiday Name</label>
                <input type="text" value={editHoliday ? editHoliday.name : newHoliday.name}
                  onChange={e => {
                    if (editHoliday) setEditHoliday({ ...editHoliday, name: e.target.value })
                    else setNewHoliday({ ...newHoliday, name: e.target.value })
                  }}
                  placeholder="e.g. Republic Day"
                  style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '5px 8px', border: '1px solid var(--border)', borderRadius: 5, width: 220 }} />
              </div>
              {editHoliday ? (
                <div style={{ display: 'flex', gap: 8 }}>
                  <button className="btn btn-primary btn-sm" onClick={saveEditHoliday}>Save</button>
                  <button className="td-side-act-btn" onClick={() => setEditHoliday(null)}>Cancel</button>
                </div>
              ) : (
                <button className="btn btn-primary btn-sm" onClick={addHoliday}>Add Holiday</button>
              )}
            </div>
          </div>

          <div style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '.85rem' }}>
              <thead><tr style={{ background: '#F9FAFB' }}>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Date</th>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Name</th>
                <th style={{ padding: '10px 14px', textAlign: 'right', borderBottom: '1px solid var(--border)' }}>Actions</th>
              </tr></thead>
              <tbody>
                {holidays.map(h => (
                  <tr key={h.id}>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>{h.date}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>{h.name}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6', textAlign: 'right' }}>
                      <button className="td-side-act-btn" style={{ marginRight: 8 }}
                        onClick={() => setEditHoliday({ id: h.id, date: h.date, name: h.name })}>Edit</button>
                      <button className="td-side-act-btn" style={{ color: '#DC2626' }} onClick={() => deleteHoliday(h.id)}>Delete</button>
                    </td>
                  </tr>
                ))}
                {holidays.length === 0 && (
                  <tr><td colSpan={3} style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)' }}>No holidays configured.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'policies' && (
        <div style={{ maxWidth: 500 }}>
          <div style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '.85rem' }}>
              <thead><tr style={{ background: '#F9FAFB' }}>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Priority</th>
                <th style={{ padding: '10px 14px', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>Duration (Working Days)</th>
                <th style={{ padding: '10px 14px', textAlign: 'right', borderBottom: '1px solid var(--border)' }}>Action</th>
              </tr></thead>
              <tbody>
                {policies.map(p => (
                  <tr key={p.id}>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6', fontWeight: 600, textTransform: 'capitalize' }}>{p.priority}</td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6' }}>
                      <input type="number" min={1} value={p.durationDays}
                        onChange={e => setPolicies(prev => prev.map(x => x.id === p.id ? { ...x, durationDays: parseInt(e.target.value) || 1 } : x))}
                        style={{ fontFamily: 'inherit', fontSize: '.85rem', padding: '4px 8px', border: '1px solid var(--border)', borderRadius: 5, width: 80 }} />
                    </td>
                    <td style={{ padding: '10px 14px', borderBottom: '1px solid #F3F4F6', textAlign: 'right' }}>
                      <button className="btn btn-primary btn-sm" onClick={() => updatePolicy(p)}>Update</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p style={{ fontSize: '.8rem', color: 'var(--text-muted)', marginTop: 8 }}>
            Duration is measured in working days (excluding weekends and holidays).
          </p>
        </div>
      )}
    </div>
  )
}
