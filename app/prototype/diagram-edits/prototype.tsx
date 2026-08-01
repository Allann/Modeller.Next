"use client";

import { useMemo, useState } from "react";

type Category = "semantic" | "view" | "layout" | "session" | "invalid";

const views = [
  { key: "behaviour", name: "Behaviour map", example: "Attendance management", nodes: ["Parent / guardian", "Record attendance", "Attendance recorded"], add: "Add behaviour", connect: "Associate behaviour with capability" },
  { key: "lifecycle", name: "Lifecycle", example: "Session report", nodes: ["Draft", "Submitted", "Withdrawn"], add: "Add lifecycle state", connect: "Add guarded transition" },
  { key: "events", name: "Causality & event flow", example: "Session reporting", nodes: ["Submit report", "Report submitted", "Processing workflow"], add: "Add event definition", connect: "Declare publication or consumption" },
  { key: "contexts", name: "Context map", example: "ACCS dependencies", nodes: ["Child Care", "ACCS", "Government integration"], add: "Add bounded context", connect: "Declare import and export" },
  { key: "structure", name: "Structural view", example: "Child Care records", nodes: ["Child", "Enrolment", "Session report"], add: "Add entity or type", connect: "Add typed relationship" },
  { key: "rules", name: "Rule decision view", example: "Determine ACCS eligibility", nodes: ["Eligibility facts", "ACCS decision", "Eligible / information required"], add: "Add rule or table row", connect: "Add typed expression or rule binding" },
] as const;

const actions: Array<{ label: string; category: Category; detail: (view: (typeof views)[number]) => string }> = [
  { label: "Add concept", category: "semantic", detail: (view) => view.add },
  { label: "Connect", category: "semantic", detail: (view) => view.connect },
  { label: "Remove from view", category: "view", detail: () => "Exclude the element from this view only" },
  { label: "Move node", category: "layout", detail: () => "Update position only" },
  { label: "Route edge", category: "layout", detail: () => "Update edge routing only" },
  { label: "Select / zoom", category: "session", detail: () => "Change temporary editor state" },
  { label: "Delete from model", category: "semantic", detail: () => "Preview impact, confirm, then delete explicitly" },
  { label: "Add deployment link", category: "invalid", detail: () => "Physical topology is outside these semantic views" },
];

const colours: Record<Category, string> = {
  semantic: "#d9ff63",
  view: "#72e1ff",
  layout: "#c5a3ff",
  session: "#ffcf70",
  invalid: "#ff7c8c",
};

export function DiagramEditPrototype() {
  const [viewIndex, setViewIndex] = useState(0);
  const [revisions, setRevisions] = useState({ semantic: 0, view: 0, layout: 0 });
  const [result, setResult] = useState<{ category: Category; detail: string } | null>(null);
  const view = views[viewIndex];
  const edges = useMemo(() => [[0, 1], [1, 2]], []);

  function apply(category: Category, detail: string) {
    setResult({ category, detail });
    if (category === "semantic" || category === "view" || category === "layout") {
      setRevisions((current) => ({ ...current, [category]: current[category] + 1 }));
    }
  }

  return (
    <main style={{ minHeight: "100vh", background: "#0b1020", color: "#edf2ff", fontFamily: "Inter, ui-sans-serif, system-ui", padding: "32px" }}>
      <div style={{ maxWidth: 1240, margin: "0 auto" }}>
        <div style={{ display: "flex", justifyContent: "space-between", gap: 24, alignItems: "end", marginBottom: 28 }}>
          <div>
            <div style={{ color: "#d9ff63", fontSize: 12, fontWeight: 800, letterSpacing: 1.8 }}>THROWAWAY PROTOTYPE · ISSUE #21</div>
            <h1 style={{ fontSize: 34, margin: "8px 0" }}>Diagram edit semantics</h1>
            <p style={{ color: "#aeb9d6", maxWidth: 760, margin: 0 }}>Every interaction must resolve to semantic model, view definition, layout, session state, or an invalid operation.</p>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            {Object.entries(revisions).map(([name, value]) => <Revision key={name} name={name} value={value} />)}
          </div>
        </div>

        <nav style={{ display: "grid", gridTemplateColumns: "repeat(6, minmax(0, 1fr))", gap: 8, marginBottom: 18 }}>
          {views.map((item, index) => (
            <button key={item.key} onClick={() => { setViewIndex(index); setResult(null); }} style={{ border: index === viewIndex ? "1px solid #d9ff63" : "1px solid #26304b", color: index === viewIndex ? "#0b1020" : "#c8d2ee", background: index === viewIndex ? "#d9ff63" : "#141b30", borderRadius: 10, padding: "11px 8px", cursor: "pointer", fontWeight: 700 }}>{item.name}</button>
          ))}
        </nav>

        <section style={{ display: "grid", gridTemplateColumns: "1.5fr 0.9fr", gap: 18 }}>
          <div style={{ background: "#10172a", border: "1px solid #26304b", borderRadius: 18, minHeight: 490, padding: 24, position: "relative", overflow: "hidden" }}>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <div><div style={{ color: "#8290b3", fontSize: 12 }}>VIEW</div><h2 style={{ margin: "4px 0" }}>{view.name}</h2></div>
              <div style={{ color: "#8290b3" }}>{view.example}</div>
            </div>
            <div style={{ position: "relative", height: 350, marginTop: 28 }}>
              {edges.map(([from, to]) => <div key={`${from}-${to}`} style={{ position: "absolute", height: 2, background: "#506184", width: "28%", top: 161, left: `${20 + from * 32}%` }} />)}
              {view.nodes.map((node, index) => (
                <div key={node} style={{ position: "absolute", left: `${4 + index * 32}%`, top: index === 1 ? 105 : 125, width: "27%", minHeight: 88, borderRadius: 14, border: "1px solid #52617f", background: index === 1 ? "#203052" : "#182238", display: "grid", placeItems: "center", padding: 14, textAlign: "center", fontWeight: 750, boxShadow: "0 14px 30px #05081680" }}>{node}</div>
              ))}
              <div style={{ position: "absolute", inset: "260px 8px auto", color: "#8290b3", borderTop: "1px dashed #34405d", paddingTop: 18 }}>Projection elements reference stable semantic IDs. Geometry is applied separately from layout state.</div>
            </div>
          </div>

          <aside style={{ background: "#10172a", border: "1px solid #26304b", borderRadius: 18, padding: 20 }}>
            <h2 style={{ marginTop: 0 }}>Try an edit</h2>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
              {actions.map((action) => <button key={action.label} onClick={() => apply(action.category, action.detail(view))} style={{ background: "#182238", border: `1px solid ${colours[action.category]}55`, color: "#edf2ff", borderRadius: 10, padding: "12px 10px", textAlign: "left", cursor: "pointer" }}>{action.label}</button>)}
            </div>
            <div style={{ marginTop: 18, minHeight: 130, borderRadius: 12, background: "#0b1020", border: `1px solid ${result ? colours[result.category] : "#26304b"}`, padding: 16 }}>
              {result ? <><div style={{ color: colours[result.category], textTransform: "uppercase", fontWeight: 900, letterSpacing: 1.3, fontSize: 12 }}>{result.category}</div><p style={{ fontSize: 18, lineHeight: 1.45 }}>{result.detail}</p><div style={{ color: "#8290b3", fontSize: 13 }}>{result.category === "semantic" ? "Validate → apply atomically → reproject" : result.category === "invalid" ? "Reject with explicit alternatives" : "Semantic meaning is unchanged"}</div></> : <div style={{ color: "#8290b3" }}>Choose an operation to see its classification and revision effect.</div>}
            </div>
            <div style={{ marginTop: 16, padding: 14, borderRadius: 12, background: "#2a2134", color: "#ffd9e1", fontSize: 14 }}><strong>Safety rule:</strong> Remove from view and Delete from model are always separate named commands.</div>
          </aside>
        </section>
      </div>
    </main>
  );
}

function Revision({ name, value }: { name: string; value: number }) {
  return <div style={{ minWidth: 84, background: "#141b30", border: "1px solid #26304b", borderRadius: 10, padding: "9px 12px", textAlign: "center" }}><div style={{ color: "#8290b3", fontSize: 11, textTransform: "uppercase" }}>{name}</div><div style={{ fontSize: 21, fontWeight: 900 }}>{value}</div></div>;
}
