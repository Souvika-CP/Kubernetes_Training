import { useState, useRef, useEffect } from 'react'
import type { FormEvent } from 'react'
import { useParams, Link } from 'react-router-dom'
import { gql } from '@apollo/client'
import {
  useGetProjectQuery,
  useGetTasksQuery,
  useUpdateTaskMutation,
  useCreateTaskMutation,
  useDeleteTaskMutation,
  useOnTaskUpdatedSubscription,
  GetTasksDocument,
  type TaskItemStatus,
  type TaskPriority,
  type GetTasksQuery,
} from '../../generated/graphql'

type Task = GetTasksQuery['tasks'][number]

const COLUMNS: { status: TaskItemStatus; label: string; accent: string; countBg: string }[] = [
  { status: 'TODO',        label: 'To Do',       accent: '#94A3B8', countBg: '#F1F5F9' },
  { status: 'IN_PROGRESS', label: 'In Progress',  accent: '#3B82F6', countBg: '#EFF6FF' },
  { status: 'IN_REVIEW',   label: 'In Review',    accent: '#F59E0B', countBg: '#FFFBEB' },
  { status: 'DONE',        label: 'Done',         accent: '#10B981', countBg: '#F0FDF4' },
]
const STATUS_ORDER = COLUMNS.map(c => c.status)

const PRIORITY: Record<TaskPriority, { bg: string; color: string; dot: string; label: string }> = {
  CRITICAL: { bg: '#FEF2F2', color: '#991B1B', dot: '#EF4444', label: 'Critical' },
  HIGH:     { bg: '#FFF7ED', color: '#9A3412', dot: '#F97316', label: 'High'     },
  MEDIUM:   { bg: '#FFFBEB', color: '#92400E', dot: '#F59E0B', label: 'Medium'   },
  LOW:      { bg: '#F8FAFC', color: '#475569', dot: '#94A3B8', label: 'Low'      },
}

// ── Task card ──────────────────────────────────────────────────────────────
function TaskCard({
  task, colIndex, totalCols, onMove, onSave, onDelete,
  isDragging, onDragStart, onDragEnd,
}: {
  task: Task; colIndex: number; totalCols: number
  onMove:      (task: Task, s: TaskItemStatus) => void
  onSave:      (task: Task, title: string, desc: string, priority: TaskPriority) => Promise<void>
  onDelete:    (id: string) => void
  isDragging:  boolean
  onDragStart: (e: React.DragEvent, task: Task) => void
  onDragEnd:   () => void
}) {
  const [editing, setEditing]   = useState(false)
  const [title, setTitle]       = useState(task.title)
  const [description, setDesc]  = useState(task.description)
  const [priority, setPriority] = useState<TaskPriority>(task.priority)
  const [saving, setSaving]     = useState(false)
  const titleRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!editing) { setTitle(task.title); setDesc(task.description); setPriority(task.priority) }
  }, [task.title, task.description, task.priority, editing])

  useEffect(() => { if (editing) titleRef.current?.focus() }, [editing])

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!title.trim()) return
    setSaving(true)
    await onSave(task, title.trim(), description.trim(), priority)
    setSaving(false)
    setEditing(false)
  }
  function cancel() { setTitle(task.title); setDesc(task.description); setPriority(task.priority); setEditing(false) }

  const p = PRIORITY[task.priority]

  if (editing) {
    return (
      <div className="card card-active animate-in" style={{ padding: '0.875rem' }}>
        <form onSubmit={handleSave} style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
          <input ref={titleRef} className="input input-sm" type="text" value={title} onChange={e => setTitle(e.target.value)} placeholder="Task title" />
          <textarea className="input input-sm" value={description} onChange={e => setDesc(e.target.value)} rows={2} placeholder="Description (optional)" />
          <select className="input input-sm" value={priority} onChange={e => setPriority(e.target.value as TaskPriority)}>
            <option value="LOW">Low</option>
            <option value="MEDIUM">Medium</option>
            <option value="HIGH">High</option>
            <option value="CRITICAL">Critical</option>
          </select>
          <div style={{ display: 'flex', gap: '6px' }}>
            <button type="button" onClick={cancel} className="btn btn-ghost btn-xs" style={{ flex: 1 }}>Cancel</button>
            <button type="submit" disabled={saving || !title.trim()} className="btn btn-primary btn-xs" style={{ flex: 1 }}>{saving ? '…' : 'Save'}</button>
          </div>
        </form>
      </div>
    )
  }

  return (
    <div
      className="card task-card"
      draggable
      onDragStart={e => onDragStart(e, task)}
      onDragEnd={onDragEnd}
      style={{
        padding: '0.875rem',
        cursor: 'grab',
        position: 'relative',
        opacity: isDragging ? 0.4 : 1,
        transform: isDragging ? 'scale(0.97)' : undefined,
        transition: 'opacity 150ms ease, transform 150ms ease, box-shadow var(--duration) var(--ease), border-color var(--duration) var(--ease)',
      }}
    >
      {/* Drag handle hint */}
      <div className="drag-handle" style={{
        position: 'absolute', left: 6, top: '50%', transform: 'translateY(-50%)',
        opacity: 0, transition: 'opacity var(--duration) var(--ease)',
        color: 'var(--text-3)', fontSize: '0.75rem', letterSpacing: '-1px',
        userSelect: 'none', pointerEvents: 'none',
      }}>⠿</div>

      {/* Action buttons */}
      <div className="task-actions" style={{
        position: 'absolute', top: 8, right: 8,
        display: 'flex', gap: 2,
        opacity: 0, transition: 'opacity var(--duration) var(--ease)',
      }}>
        <button
          onClick={e => { e.stopPropagation(); setEditing(true) }}
          onMouseDown={e => e.stopPropagation()}
          className="btn-icon brand" title="Edit"
          style={{ width: 24, height: 24 }}
        >
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
          </svg>
        </button>
        <button
          onClick={e => { e.stopPropagation(); onDelete(task.id) }}
          onMouseDown={e => e.stopPropagation()}
          className="btn-icon danger" title="Delete"
          style={{ width: 24, height: 24 }}
        >
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="3,6 5,6 21,6"/><path d="M19 6l-1 14a2 2 0 01-2 2H8a2 2 0 01-2-2L5 6"/><path d="M10 11v6M14 11v6"/>
          </svg>
        </button>
      </div>

      <p style={{
        fontWeight: 600, fontSize: '0.8125rem', color: 'var(--text-1)',
        lineHeight: 1.45, marginBottom: task.description ? 6 : 10,
        paddingRight: 40, paddingLeft: 4,
      }}>
        {task.title}
      </p>

      {task.description && (
        <p style={{
          fontSize: '0.75rem', color: 'var(--text-3)', lineHeight: 1.5, marginBottom: 10, paddingLeft: 4,
          display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden',
        }}>
          {task.description}
        </p>
      )}

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span className="badge" style={{ background: p.bg, color: p.color }}>
          <span className="badge-dot" style={{ background: p.dot }} />
          {p.label}
        </span>

        {/* Arrow buttons (fallback for keyboard/accessibility) */}
        <div className="move-btns" style={{ display: 'flex', gap: 2, opacity: 0, transition: 'opacity var(--duration) var(--ease)' }}>
          {colIndex > 0 && (
            <button
              onClick={e => { e.stopPropagation(); onMove(task, STATUS_ORDER[colIndex - 1]) }}
              onMouseDown={e => e.stopPropagation()}
              className="btn-icon" title={`← ${COLUMNS[colIndex-1].label}`}
              style={{ width: 24, height: 24, fontSize: '0.8rem', fontWeight: 700 }}
            >←</button>
          )}
          {colIndex < totalCols - 1 && (
            <button
              onClick={e => { e.stopPropagation(); onMove(task, STATUS_ORDER[colIndex + 1]) }}
              onMouseDown={e => e.stopPropagation()}
              className="btn-icon brand" title={`${COLUMNS[colIndex+1].label} →`}
              style={{ width: 24, height: 24, fontSize: '0.8rem', fontWeight: 700 }}
            >→</button>
          )}
        </div>
      </div>
    </div>
  )
}

function SkeletonCard() {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius)', padding: '0.875rem' }}>
      <div className="skeleton" style={{ height: 13, borderRadius: 5, width: '75%', marginBottom: 8 }} />
      <div className="skeleton" style={{ height: 11, borderRadius: 5, width: '100%', marginBottom: 5 }} />
      <div className="skeleton" style={{ height: 11, borderRadius: 5, width: '55%', marginBottom: 10 }} />
      <div className="skeleton" style={{ height: 18, borderRadius: 99, width: 55 }} />
    </div>
  )
}

// ── Board page ─────────────────────────────────────────────────────────────
export default function BoardPage() {
  const { id } = useParams<{ id: string }>()

  const { data: projData } = useGetProjectQuery({ variables: { id: id! }, skip: !id })
  const { data: tasksData, loading: tasksLoading, error: tasksError } = useGetTasksQuery({ variables: { projectId: id! }, skip: !id })

  const [updateTask] = useUpdateTaskMutation()
  const [createTask, { loading: creating }] = useCreateTaskMutation({
    refetchQueries: [{ query: GetTasksDocument, variables: { projectId: id } }],
  })
  const [deleteTask] = useDeleteTaskMutation({
    refetchQueries: [{ query: GetTasksDocument, variables: { projectId: id } }],
  })

  useOnTaskUpdatedSubscription({
    variables: { projectId: id! },
    skip: !id,
    onData({ client, data }) {
      const task = data.data?.onTaskUpdated
      if (!task) return
      client.cache.writeFragment({
        id: client.cache.identify({ __typename: 'TaskItem', id: task.id }),
        fragment: gql`fragment Sub on TaskItem { id status title priority updatedAt }`,
        data: task,
      })
    },
  })

  // ── Drag state ─────────────────────────────────────────────────────────
  const [draggingTask, setDraggingTask]     = useState<Task | null>(null)
  const [dragOverStatus, setDragOverStatus] = useState<TaskItemStatus | null>(null)
  // track with a ref too so onDrop can always read the current value
  const draggingTaskRef = useRef<Task | null>(null)

  function handleDragStart(e: React.DragEvent, task: Task) {
    draggingTaskRef.current = task
    setDraggingTask(task)
    e.dataTransfer.effectAllowed = 'move'
    // Encode the task id so drop handler has a fallback
    e.dataTransfer.setData('taskId', task.id)
  }

  function handleDragEnd() {
    draggingTaskRef.current = null
    setDraggingTask(null)
    setDragOverStatus(null)
  }

  function handleDragOver(e: React.DragEvent, status: TaskItemStatus) {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'move'
    setDragOverStatus(status)
  }

  function handleDragLeave(e: React.DragEvent) {
    // Only clear if we're leaving the column container itself
    if (!e.currentTarget.contains(e.relatedTarget as Node)) {
      setDragOverStatus(null)
    }
  }

  function handleDrop(e: React.DragEvent, targetStatus: TaskItemStatus) {
    e.preventDefault()
    const task = draggingTaskRef.current
    if (task && task.status !== targetStatus) {
      moveTask(task, targetStatus)
    }
    draggingTaskRef.current = null
    setDraggingTask(null)
    setDragOverStatus(null)
  }

  // ── Task actions ───────────────────────────────────────────────────────
  const [newTaskColumn, setNewTaskColumn]       = useState<TaskItemStatus | null>(null)
  const [newTaskTitle, setNewTaskTitle]         = useState('')
  const [newTaskPriority, setNewTaskPriority]   = useState<TaskPriority>('MEDIUM')

  async function handleCreateTask(e: FormEvent) {
    e.preventDefault()
    if (!newTaskTitle.trim() || !newTaskColumn || !id) return
    await createTask({ variables: { input: { title: newTaskTitle.trim(), description: '', projectId: id, priority: newTaskPriority } } })
    setNewTaskColumn(null)
  }

  function moveTask(task: Task, newStatus: TaskItemStatus) {
    updateTask({
      variables: { input: { id: task.id, title: task.title, description: task.description, status: newStatus, priority: task.priority, assigneeId: task.assigneeId ?? null, dueDate: task.dueDate ?? null, tags: task.tags ?? [] } },
      optimisticResponse: { updateTask: { id: task.id, title: task.title, description: task.description, status: newStatus, priority: task.priority, assigneeId: task.assigneeId ?? null, dueDate: task.dueDate ?? null, tags: task.tags ?? [], updatedAt: new Date().toISOString() } } as any,
    })
  }

  async function handleSaveTask(task: Task, title: string, description: string, priority: TaskPriority) {
    await updateTask({ variables: { input: { id: task.id, title, description, status: task.status, priority, assigneeId: task.assigneeId ?? null, dueDate: task.dueDate ?? null, tags: task.tags ?? [] } } })
  }

  function handleDeleteTask(taskId: string) {
    if (!confirm('Delete this task?')) return
    deleteTask({ variables: { id: taskId } })
  }

  const tasks   = tasksData?.tasks ?? []
  const project = projData?.project

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', display: 'flex', flexDirection: 'column' }}>

      {/* Nav */}
      <nav className="nav-glass" style={{ position: 'sticky', top: 0, zIndex: 40, padding: '0 1.5rem', height: 52, display: 'flex', alignItems: 'center', gap: 10 }}>
        <Link to="/" style={{ display: 'flex', alignItems: 'center', gap: 6, textDecoration: 'none', color: 'var(--text-3)', fontSize: '0.8125rem', fontWeight: 500 }}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12,19 5,12 12,5"/></svg>
          Dashboard
        </Link>
        <span style={{ color: 'var(--border)', userSelect: 'none' }}>/</span>
        <span style={{ fontWeight: 700, fontSize: '0.9375rem', letterSpacing: '-0.01em', color: 'var(--text-1)' }}>
          {project?.name ?? '…'}
        </span>
        {project && (
          <span className="badge" style={{ background: '#F0FDF4', color: '#065F46', marginLeft: 2 }}>
            <span className="badge-dot" style={{ background: '#10B981' }} />
            {project.status.charAt(0) + project.status.slice(1).toLowerCase()}
          </span>
        )}
      </nav>

      {/* Board */}
      <div style={{ flex: 1, padding: '1.5rem', overflowX: 'auto' }}>
        {tasksError && (
          <div style={{ background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: 'var(--radius-lg)', padding: '0.875rem 1rem', marginBottom: '1rem', fontSize: '0.875rem', color: '#991B1B' }}>
            Failed to load tasks — {tasksError.message}
          </div>
        )}

        <div style={{ display: 'flex', gap: '1rem', minWidth: 'max-content', alignItems: 'flex-start', paddingBottom: '1rem' }}>
          {COLUMNS.map((col, colIndex) => {
            const colTasks    = tasks.filter(t => t.status === col.status)
            const isDropZone  = dragOverStatus === col.status && draggingTask?.status !== col.status
            const isDragSource = draggingTask !== null

            return (
              <div
                key={col.status}
                onDragOver={e => handleDragOver(e, col.status)}
                onDragLeave={handleDragLeave}
                onDrop={e => handleDrop(e, col.status)}
                style={{
                  width: 280,
                  display: 'flex',
                  flexDirection: 'column',
                  background: 'var(--surface)',
                  border: `1px solid ${isDropZone ? col.accent : 'var(--border)'}`,
                  borderRadius: 'var(--radius-xl)',
                  boxShadow: isDropZone
                    ? `0 0 0 2px ${col.accent}40, var(--shadow-md)`
                    : 'var(--shadow-xs)',
                  overflow: 'hidden',
                  transition: 'border-color 120ms ease, box-shadow 120ms ease',
                }}
              >
                {/* Column header */}
                <div style={{
                  padding: '12px 14px',
                  borderBottom: '1px solid var(--border)',
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  borderTop: `3px solid ${col.accent}`,
                }}>
                  <span style={{ fontWeight: 700, fontSize: '0.8125rem', color: 'var(--text-1)', letterSpacing: '-0.01em' }}>{col.label}</span>
                  <span style={{ fontSize: '0.75rem', fontWeight: 700, color: col.accent, background: col.countBg, padding: '2px 8px', borderRadius: 99, minWidth: 24, textAlign: 'center' }}>
                    {tasksLoading ? '…' : colTasks.length}
                  </span>
                </div>

                {/* Drop zone body */}
                <div style={{
                  padding: '10px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '8px',
                  minHeight: 160,
                  background: isDropZone ? `${col.accent}08` : isDragSource ? '#FAFBFD' : '#FAFBFC',
                  transition: 'background 120ms ease',
                }}>
                  {/* Drop indicator shown when dragging over an empty / different column */}
                  {isDropZone && (
                    <div style={{
                      border: `2px dashed ${col.accent}`,
                      borderRadius: 'var(--radius)',
                      height: 60,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: '0.75rem',
                      color: col.accent,
                      fontWeight: 600,
                      background: `${col.accent}0A`,
                      animation: 'fade-in 120ms ease both',
                    }}>
                      Drop here
                    </div>
                  )}

                  {tasksLoading
                    ? [1,2].map(i => <SkeletonCard key={i} />)
                    : colTasks.map(task => (
                        <TaskCard
                          key={task.id}
                          task={task}
                          colIndex={colIndex}
                          totalCols={COLUMNS.length}
                          onMove={moveTask}
                          onSave={handleSaveTask}
                          onDelete={handleDeleteTask}
                          isDragging={draggingTask?.id === task.id}
                          onDragStart={handleDragStart}
                          onDragEnd={handleDragEnd}
                        />
                      ))
                  }
                  {!tasksLoading && colTasks.length === 0 && !isDropZone && (
                    <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1.5rem 0' }}>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-3)' }}>No tasks</span>
                    </div>
                  )}
                </div>

                {/* Footer */}
                <div style={{ padding: '8px 10px', borderTop: '1px solid var(--border)', background: 'var(--surface)' }}>
                  {newTaskColumn === col.status ? (
                    <form onSubmit={handleCreateTask} className="animate-in" style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                      <input autoFocus className="input input-sm" type="text" placeholder="Task title…" value={newTaskTitle} onChange={e => setNewTaskTitle(e.target.value)} />
                      <select className="input input-sm" value={newTaskPriority} onChange={e => setNewTaskPriority(e.target.value as TaskPriority)}>
                        <option value="LOW">Low</option>
                        <option value="MEDIUM">Medium</option>
                        <option value="HIGH">High</option>
                        <option value="CRITICAL">Critical</option>
                      </select>
                      <div style={{ display: 'flex', gap: '6px' }}>
                        <button type="submit" disabled={creating || !newTaskTitle.trim()} className="btn btn-primary btn-xs" style={{ flex: 1 }}>{creating ? '…' : 'Add'}</button>
                        <button type="button" onClick={() => setNewTaskColumn(null)} className="btn btn-ghost btn-xs" style={{ flex: 1 }}>Cancel</button>
                      </div>
                    </form>
                  ) : (
                    <button
                      onClick={() => { setNewTaskColumn(col.status); setNewTaskTitle(''); setNewTaskPriority('MEDIUM') }}
                      style={{
                        width: '100%', background: 'transparent', border: 'none', cursor: 'pointer',
                        display: 'flex', alignItems: 'center', gap: 6, padding: '6px 4px',
                        fontSize: '0.8125rem', color: 'var(--text-3)', borderRadius: 'var(--radius-sm)',
                        transition: 'all var(--duration) var(--ease)', fontFamily: 'var(--font-sans)',
                      }}
                      className="add-task-btn"
                    >
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                      Add task
                    </button>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      </div>

      <style>{`
        .task-card:hover .task-actions  { opacity: 1 !important; }
        .task-card:hover .move-btns     { opacity: 1 !important; }
        .task-card:hover .drag-handle   { opacity: 1 !important; }
        .task-card:active               { cursor: grabbing !important; }
        .add-task-btn:hover             { color: var(--brand) !important; background: var(--brand-light) !important; }
      `}</style>
    </div>
  )
}
