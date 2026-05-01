"use client";

export default function ExplorerPage() {
  return (
    <div className="fade-in max-w-2xl">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">API Explorer</h1>
        <p className="text-sm mt-2" style={{ color: '#71717a' }}>Test and explore your API endpoints</p>
      </div>
      <div className="rounded-2xl glass-card p-10 text-center relative overflow-hidden">
        {/* Background glow */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-48 h-48 rounded-full"
          style={{ background: 'radial-gradient(circle, rgba(99, 102, 241, 0.08), transparent)', filter: 'blur(40px)' }}
        />

        <div className="relative">
          <div className="w-14 h-14 mx-auto mb-5 rounded-2xl flex items-center justify-center"
            style={{ background: 'rgba(99, 102, 241, 0.06)', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#52525b" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
            </svg>
          </div>
          <p className="text-sm font-medium mb-1.5" style={{ color: '#a1a1aa' }}>
            Select a project to explore its API endpoints.
          </p>
          <p className="text-[13px]" style={{ color: '#52525b' }}>
            Go to a project detail page and use the API Explorer tab.
          </p>
        </div>
      </div>
    </div>
  );
}
