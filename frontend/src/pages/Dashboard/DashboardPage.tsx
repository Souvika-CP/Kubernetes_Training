import { useState, useRef, useEffect } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../apollo/AuthContext'
import {
  useGetWorkspacesQuery,
  useGetProjectsQuery,
  useCreateWorkspaceMutation,
  useCreateProjectMutation,
  useUpdateProjectMutation,
  useDeleteProjectMutation,
  GetProjectsDocument,
  type ProjectStatus,
  type GetProjectsQuery,
} from '../../generated/graphql'

type Project = GetProjectsQuery['projects'][number]

function Avatar({ email }: { email: string }) {
  return (
    <div style={{
      width: 32, height: 32, borderRadius: '50%',
      background: 'linear-gradient(135deg, #6366F1, #8B5CF6)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      color: '#fff', fontSize: '0.75rem', fontWeight: 700,
      userSelect: 'none', flexShrink: 0,
      boxShadow: '0 2px 8px rgba(99,102,241,0.3)',
    }}>
      {email.slice(0, 2).toUpperCase()}
    </div>
  )
}

const STATUS_CONFIG: Record<string, { dot: string; bg: string; color: string; label: string }> = {
  ACTIVE:    { dot: '#10B981', bg: '#F0FDF4', color: '#065F46', label: 'Active'    },
  ON_HOLD:   { dot: '#F59E0B', bg: '#FFFBEB', color: '#92400E', label: 'On Hold'   },
  ARCHIVED:  { dot: '#94A3B8', bg: '#F8FAFC', color: '#475569', label: 'Archived'  },
  COMPLETED: { dot: '#3B82F6', bg: '#EFF6FF', color: '#1E40AF', label: 'Completed' },
}

function StatusBadge({ status }: { status: ProjectStatus }) {
  const c = STATUS_CONFIG[status] ?? STATUS_CONFIG.ARCHIVED
  return (
    <span className="badge" style={{ background: c.bg, color: c.color }}>
      <span className="badge-dot" style={{ background: c.dot }} />
      {c.label}
    </span>
  )
}

function ProjectCard({
  project, onSave, onDelete,
}: {
  project: Project
  onSave: (id: string, name: string, description: string, status: ProjectStatus) => Promise<void>
  onDelete: (id: string) => void
}) {
  const [editing, setEditing] = useState(false)
  const [name, setName]             = useState(project.name)
  const [description, setDescription] = useState(project.description)
  const [saving, setSaving]         = useState(false)
  const nameRef = useRef<HTMLInputElement>(null)

  useEffect(() => { if (editing) nameRef.current?.focus() }, [editing])

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    setSaving(true)
    await onSave(project.id, name.trim(), description.trim(), project.status)
    setSaving(false)
    setEditing(false)
  }
  function cancel() { setName(project.name); setDescription(project.description); setEditing(false) }

  if (editing) {
    return (
      <div className="card card-active animate-in" style={{ padding: '1.25rem' }}>
        <form onSubmit={handleSave} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <input
            ref={nameRef}
            className="input"
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="Project name"
          />
          <textarea
            className="input"
            value={description}
            onChange={e => setDescription(e.target.value)}
            rows={2}
            placeholder="Description (optional)"
          />
          <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
            <button type="button" onClick={cancel} className="btn btn-ghost btn-sm">Cancel</button>
            <button type="submit" disabled={saving || !name.trim()} className="btn btn-primary btn-sm">
              {saving ? 'Saving…' : 'Save changes'}
            </button>
          </div>
        </form>
      </div>
    )
  }

  return (
    <div className="card" style={{ padding: 0, overflow: 'hidden', position: 'relative' }}>
      {/* Top accent bar */}
      <div style={{
        height: 3,
        background: 'linear-gradient(90deg, #6366F1 0%, #8B5CF6 100%)',
      }} />
      <div
        style={{ padding: '1.25rem 1.25rem 1rem' }}
        className="project-card-body"
      >
        {/* Actions */}
        <div className="project-actions" style={{
          position: 'absolute', top: '14px', right: '12px',
          display: 'flex', gap: '4px',
          opacity: 0, transition: 'opacity var(--duration) var(--ease)',
        }}>
          <button onClick={() => setEditing(true)} className="btn-icon brand" title="Edit">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
            </svg>
          </button>
          <button onClick={() => onDelete(project.id)} className="btn-icon danger" title="Delete">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="3,6 5,6 21,6"/><path d="M19 6l-1 14a2 2 0 01-2 2H8a2 2 0 01-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 011-1h4a1 1 0 011 1v2"/>
            </svg>
          </button>
        </div>

        <Link
          to={`/projects/${project.id}`}
          style={{
            display: 'block', fontWeight: 600, fontSize: '0.9375rem',
            color: 'var(--text-1)', marginBottom: '6px', letterSpacing: '-0.01em',
            textDecoration: 'none',
          }}
          className="project-name-link"
        >
          {project.name}
        </Link>

        {project.description && (
          <p className="t-body" style={{
            fontSize: '0.8125rem', marginBottom: '1rem',
            display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden',
          }}>
            {project.description}
          </p>
        )}

        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 'auto', paddingTop: '0.75rem', borderTop: '1px solid var(--border)' }}>
          <StatusBadge status={project.status} />
          <Link
            to={`/projects/${project.id}`}
            style={{ fontSize: '0.75rem', color: 'var(--brand)', fontWeight: 600, textDecoration: 'none' }}
          >
            Open →
          </Link>
        </div>
      </div>
    </div>
  )
}

function SkeletonCard() {
  return (
    <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
      <div style={{ height: 3, background: '#E2E8F0' }} />
      <div style={{ padding: '1.25rem' }}>
        <div className="skeleton" style={{ height: 16, borderRadius: 6, width: '65%', marginBottom: 10 }} />
        <div className="skeleton" style={{ height: 12, borderRadius: 6, width: '100%', marginBottom: 6 }} />
        <div className="skeleton" style={{ height: 12, borderRadius: 6, width: '80%', marginBottom: 16 }} />
        <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: '12px', borderTop: '1px solid var(--border)' }}>
          <div className="skeleton" style={{ height: 20, borderRadius: 99, width: 60 }} />
          <div className="skeleton" style={{ height: 14, borderRadius: 6, width: 50 }} />
        </div>
      </div>
    </div>
  )
}

function EmptyState({ icon, title, message }: { icon: React.ReactNode; title: string; message: string }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: 280, textAlign: 'center', padding: '2rem' }}>
      <div style={{
        width: 56, height: 56, borderRadius: 16, background: 'var(--surface-2)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        marginBottom: '1rem', color: 'var(--text-3)',
      }}>
        {icon}
      </div>
      <div style={{ fontWeight: 600, color: 'var(--text-1)', marginBottom: 4 }}>{title}</div>
      <div style={{ fontSize: '0.875rem', color: 'var(--text-3)', maxWidth: 260 }}>{message}</div>
    </div>
  )
}

export default function DashboardPage() {
  const { user, logout } = useAuth()

  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string | null>(null)
  const [showNewWorkspace, setShowNewWorkspace] = useState(false)
  const [newWorkspaceName, setNewWorkspaceName] = useState('')
  const [showNewProject, setShowNewProject] = useState(false)
  const [newProjectName, setNewProjectName] = useState('')
  const [newProjectDesc, setNewProjectDesc] = useState('')

  const { data: wsData, loading: wsLoading } = useGetWorkspacesQuery()

  useEffect(() => {
    if (!selectedWorkspaceId && wsData?.workspaces && wsData.workspaces.length > 0)
      setSelectedWorkspaceId(wsData.workspaces[0].id)
  }, [wsData, selectedWorkspaceId])

  const { data: projData, loading: projLoading, error: projError } = useGetProjectsQuery({
    variables: { workspaceId: selectedWorkspaceId! },
    skip: !selectedWorkspaceId,
  })

  const [createWorkspace, { loading: creatingWs }] = useCreateWorkspaceMutation({
    refetchQueries: ['GetWorkspaces'],
    onCompleted(data) {
      setSelectedWorkspaceId(data.createWorkspace.id)
      setNewWorkspaceName('')
      setShowNewWorkspace(false)
    },
  })
  const [createProject, { loading: creatingProj }] = useCreateProjectMutation({
    refetchQueries: [{ query: GetProjectsDocument, variables: { workspaceId: selectedWorkspaceId } }],
    onCompleted() { setNewProjectName(''); setNewProjectDesc(''); setShowNewProject(false) },
  })
  const [updateProject] = useUpdateProjectMutation()
  const [deleteProject] = useDeleteProjectMutation({
    refetchQueries: [{ query: GetProjectsDocument, variables: { workspaceId: selectedWorkspaceId } }],
  })

  async function handleCreateWorkspace(e: FormEvent) {
    e.preventDefault()
    if (!newWorkspaceName.trim()) return
    await createWorkspace({ variables: { input: { name: newWorkspaceName.trim(), ownerId: user!.userId } } })
  }
  async function handleCreateProject(e: FormEvent) {
    e.preventDefault()
    if (!newProjectName.trim() || !selectedWorkspaceId) return
    await createProject({ variables: { input: { name: newProjectName.trim(), description: newProjectDesc.trim(), workspaceId: selectedWorkspaceId, ownerId: user!.userId } } })
  }
  async function handleUpdateProject(id: string, name: string, description: string, status: ProjectStatus) {
    await updateProject({ variables: { input: { id, name, description, status } } })
  }
  function handleDeleteProject(id: string) {
    if (!confirm('Delete this project and all its tasks? This cannot be undone.')) return
    deleteProject({ variables: { id } })
  }

  const workspaces = wsData?.workspaces ?? []
  const projects   = projData?.projects ?? []
  const selectedWs = workspaces.find(w => w.id === selectedWorkspaceId)

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', display: 'flex', flexDirection: 'column' }}>

      {/* ── Nav ── */}
      <nav className="nav-glass" style={{ position: 'sticky', top: 0, zIndex: 40, padding: '0 1.5rem', height: 56, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div style={{
            width: 30, height: 30, borderRadius: 9,
            background: 'linear-gradient(135deg, #6366F1, #8B5CF6)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 2px 8px rgba(99,102,241,0.35)',
          }}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11"/>
            </svg>
          </div>
          <span style={{ fontWeight: 700, fontSize: '0.9375rem', letterSpacing: '-0.02em', color: 'var(--text-1)' }}>TaskFlow</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span style={{ fontSize: '0.8125rem', color: 'var(--text-3)' }}>{user?.email}</span>
          <Avatar email={user?.email ?? 'U'} />
          <button onClick={logout} className="btn btn-ghost btn-sm">Sign out</button>
        </div>
      </nav>

      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>

        {/* ── Sidebar ── */}
        <aside style={{
          width: 240, flexShrink: 0, background: 'var(--surface)',
          borderRight: '1px solid var(--border)',
          display: 'flex', flexDirection: 'column', padding: '1.25rem 0.75rem',
          gap: '2px',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 6px', marginBottom: '0.5rem' }}>
            <span className="t-label">Workspaces</span>
            <button
              onClick={() => setShowNewWorkspace(v => !v)}
              className="btn-icon brand"
              title="New workspace"
              style={{ width: 24, height: 24, fontSize: '1.1rem' }}
            >
              +
            </button>
          </div>

          {showNewWorkspace && (
            <form onSubmit={handleCreateWorkspace} className="animate-in" style={{ margin: '4px 0 8px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
              <input autoFocus className="input input-sm" type="text" placeholder="Workspace name" value={newWorkspaceName} onChange={e => setNewWorkspaceName(e.target.value)} />
              <div style={{ display: 'flex', gap: '6px' }}>
                <button type="submit" disabled={creatingWs || !newWorkspaceName.trim()} className="btn btn-primary btn-xs" style={{ flex: 1 }}>
                  {creatingWs ? '…' : 'Create'}
                </button>
                <button type="button" onClick={() => { setShowNewWorkspace(false); setNewWorkspaceName('') }} className="btn btn-ghost btn-xs" style={{ flex: 1 }}>
                  Cancel
                </button>
              </div>
            </form>
          )}

          {wsLoading
            ? [1,2,3].map(i => <div key={i} className="skeleton" style={{ height: 34, borderRadius: 'var(--radius)', margin: '2px 0' }} />)
            : workspaces.map(ws => (
                <button
                  key={ws.id}
                  onClick={() => setSelectedWorkspaceId(ws.id)}
                  className={`nav-item${ws.id === selectedWorkspaceId ? ' active' : ''}`}
                >
                  {ws.name}
                </button>
              ))
          }
          {!wsLoading && workspaces.length === 0 && (
            <p style={{ fontSize: '0.8125rem', color: 'var(--text-3)', padding: '8px 10px' }}>No workspaces yet</p>
          )}
        </aside>

        {/* ── Main ── */}
        <main style={{ flex: 1, overflowY: 'auto', padding: '2rem 2.5rem' }}>
          {!selectedWorkspaceId ? (
            <EmptyState
              icon={<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>}
              title="No workspace selected"
              message="Create or select a workspace from the sidebar to get started."
            />
          ) : (
            <>
              {/* Page header */}
              <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '2rem' }}>
                <div>
                  <h1 className="t-heading" style={{ marginBottom: 4 }}>{selectedWs?.name}</h1>
                  <p style={{ fontSize: '0.875rem', color: 'var(--text-3)' }}>
                    {projects.length} project{projects.length !== 1 ? 's' : ''}
                  </p>
                </div>
                <button onClick={() => setShowNewProject(v => !v)} className="btn btn-primary">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                  New Project
                </button>
              </div>

              {/* New project form */}
              {showNewProject && (
                <div className="card animate-in" style={{ padding: '1.5rem', marginBottom: '1.5rem' }}>
                  <div style={{ fontWeight: 600, marginBottom: '1rem', color: 'var(--text-1)' }}>New project</div>
                  <form onSubmit={handleCreateProject} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                    <input autoFocus className="input" type="text" placeholder="Project name" value={newProjectName} onChange={e => setNewProjectName(e.target.value)} />
                    <input className="input" type="text" placeholder="Description (optional)" value={newProjectDesc} onChange={e => setNewProjectDesc(e.target.value)} />
                    <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                      <button type="button" onClick={() => { setShowNewProject(false); setNewProjectName(''); setNewProjectDesc('') }} className="btn btn-ghost btn-sm">Cancel</button>
                      <button type="submit" disabled={creatingProj || !newProjectName.trim()} className="btn btn-primary btn-sm">
                        {creatingProj ? 'Creating…' : 'Create project'}
                      </button>
                    </div>
                  </form>
                </div>
              )}

              {projError && (
                <div style={{ background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: 'var(--radius-lg)', padding: '1rem', marginBottom: '1.5rem', fontSize: '0.875rem', color: '#991B1B' }}>
                  Failed to load projects — {projError.message}
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '1rem' }}>
                {projLoading
                  ? [1,2,3].map(i => <SkeletonCard key={i} />)
                  : projects.map(p => (
                      <ProjectCard key={p.id} project={p} onSave={handleUpdateProject} onDelete={handleDeleteProject} />
                    ))
                }
              </div>

              {!projLoading && !projError && projects.length === 0 && (
                <EmptyState
                  icon={<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14,2 14,8 20,8"/></svg>}
                  title="No projects yet"
                  message="Create your first project to start organising work."
                />
              )}
            </>
          )}
        </main>
      </div>

      {/* Card hover styles injected globally */}
      <style>{`
        .project-card-body:hover .project-actions { opacity: 1 !important; }
        .project-name-link:hover { color: var(--brand) !important; }
      `}</style>
    </div>
  )
}
