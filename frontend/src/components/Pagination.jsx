export default function Pagination({ page, totalPages, total, start, onPageChange }) {
  if (total === 0) return null

  const pages = []
  for (let i = 1; i <= totalPages; i++) pages.push(i)

  return (
    <div className="pagination">
      <span className="pagination-info">
        Showing {total ? start + 1 : 0} to {Math.min(start + 10, total)} of {total} tickets
      </span>
      <div className="pagination-controls">
        <button
          className="pagination-btn"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          <i className="fas fa-chevron-left" />
        </button>
        {pages.map(p => (
          <button
            key={p}
            className={`pagination-btn${p === page ? ' active' : ''}`}
            onClick={() => onPageChange(p)}
          >
            {p}
          </button>
        ))}
        <button
          className="pagination-btn"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          <i className="fas fa-chevron-right" />
        </button>
      </div>
    </div>
  )
}
