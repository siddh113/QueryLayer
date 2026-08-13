"use client";

export type SavePhase = "idle" | "generating" | "previewing" | "applying" | "done" | "error";

interface SpecSaveProgressIndicatorProps {
  phase: SavePhase;
  errorMessage?: string;
}

const STEPS: { key: SavePhase; label: string }[] = [
  { key: "generating", label: "Generating" },
  { key: "previewing", label: "Reviewing" },
  { key: "applying", label: "Applying" },
  { key: "done", label: "Done" },
];

const PHASE_ORDER: Record<SavePhase, number> = {
  idle: -1,
  generating: 0,
  previewing: 1,
  applying: 2,
  done: 3,
  error: 99,
};

export default function SpecSaveProgressIndicator({ phase, errorMessage }: SpecSaveProgressIndicatorProps) {
  if (phase === "idle") return null;

  const currentIndex = PHASE_ORDER[phase];

  return (
    <div className="rounded-xl px-5 py-4"
      style={{
        background: phase === "error" ? "var(--error-bg)" : "var(--glow-cyan)",
        border: `1px solid ${phase === "error" ? "var(--error-border)" : "rgba(255, 107, 74, 0.15)"}`,
      }}
    >
      {phase === "error" ? (
        <div className="flex items-center gap-2 text-sm" style={{ color: "var(--error-text)" }}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10" /><line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
          {errorMessage || "An error occurred"}
        </div>
      ) : (
        <div className="flex items-center gap-0">
          {STEPS.map((step, idx) => {
            const stepIndex = PHASE_ORDER[step.key];
            const isCompleted = stepIndex < currentIndex;
            const isActive = stepIndex === currentIndex;

            return (
              <div key={step.key} className="flex items-center">
                <div className="flex items-center gap-1.5">
                  <div
                    className="w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{
                      background: isCompleted
                        ? "rgba(34, 197, 94, 0.15)"
                        : isActive
                        ? "rgba(255, 107, 74, 0.2)"
                        : "var(--hover-nav-bg)",
                      border: `1.5px solid ${
                        isCompleted
                          ? "rgba(34, 197, 94, 0.4)"
                          : isActive
                          ? "rgba(255, 107, 74, 0.5)"
                          : "var(--border)"
                      }`,
                    }}
                  >
                    {isCompleted ? (
                      <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="var(--success-text)" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    ) : isActive ? (
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{
                          background: "var(--accent-cyan)",
                          animation: "pulse 1.5s ease-in-out infinite",
                        }}
                      />
                    ) : (
                      <span className="w-1.5 h-1.5 rounded-full" style={{ background: "var(--text-faint)" }} />
                    )}
                  </div>
                  <span
                    className="text-xs font-medium"
                    style={{
                      color: isCompleted ? "var(--success-text)" : isActive ? "var(--accent-cyan-light)" : "var(--text-faint)",
                    }}
                  >
                    {step.label}
                  </span>
                </div>
                {idx < STEPS.length - 1 && (
                  <div
                    className="w-8 h-px mx-2 flex-shrink-0"
                    style={{
                      background: stepIndex < currentIndex
                        ? "rgba(34, 197, 94, 0.3)"
                        : "var(--border)",
                    }}
                  />
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
