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
        background: phase === "error"
          ? "rgba(239, 68, 68, 0.06)"
          : "rgba(99, 102, 241, 0.06)",
        border: `1px solid ${phase === "error" ? "rgba(239, 68, 68, 0.15)" : "rgba(99, 102, 241, 0.15)"}`,
      }}
    >
      {phase === "error" ? (
        <div className="flex items-center gap-2 text-sm" style={{ color: "#f87171" }}>
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
            const isPending = stepIndex > currentIndex;

            return (
              <div key={step.key} className="flex items-center">
                <div className="flex items-center gap-1.5">
                  <div
                    className="w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{
                      background: isCompleted
                        ? "rgba(34, 197, 94, 0.15)"
                        : isActive
                        ? "rgba(99, 102, 241, 0.2)"
                        : "rgba(255, 255, 255, 0.04)",
                      border: `1.5px solid ${
                        isCompleted
                          ? "rgba(34, 197, 94, 0.4)"
                          : isActive
                          ? "rgba(99, 102, 241, 0.5)"
                          : "rgba(255, 255, 255, 0.1)"
                      }`,
                    }}
                  >
                    {isCompleted ? (
                      <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="#4ade80" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    ) : isActive ? (
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{
                          background: "#818cf8",
                          animation: "pulse 1.5s ease-in-out infinite",
                        }}
                      />
                    ) : (
                      <span className="w-1.5 h-1.5 rounded-full" style={{ background: "rgba(255,255,255,0.15)" }} />
                    )}
                  </div>
                  <span
                    className="text-xs font-medium"
                    style={{
                      color: isCompleted ? "#4ade80" : isActive ? "#a5b4fc" : "rgba(255,255,255,0.25)",
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
                        : "rgba(255, 255, 255, 0.08)",
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
