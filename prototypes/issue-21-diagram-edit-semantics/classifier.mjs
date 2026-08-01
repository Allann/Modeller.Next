export const views = [
  {
    id: "behaviour-map",
    label: "Behaviour map",
    example: "Attendance management → Record parent attendance",
    semanticAdd: "Add behaviour",
    semanticConnect: "Associate behaviour with capability",
  },
  {
    id: "lifecycle",
    label: "Lifecycle",
    example: "Session report: Draft → Submitted → Withdrawn",
    semanticAdd: "Add lifecycle state",
    semanticConnect: "Add guarded transition",
  },
  {
    id: "causality-event-flow",
    label: "Causality and event flow",
    example: "Submit session report → Session report submitted → processing workflow",
    semanticAdd: "Add event definition",
    semanticConnect: "Declare event publication or consumption",
  },
  {
    id: "context-map",
    label: "Context map",
    example: "ACCS imports enrolment facts from Child Care",
    semanticAdd: "Add bounded context",
    semanticConnect: "Declare import and matching export",
  },
  {
    id: "structural",
    label: "Structural view",
    example: "Child ↔ Enrolment ↔ Session report",
    semanticAdd: "Add entity or declared type",
    semanticConnect: "Add typed semantic relationship",
  },
  {
    id: "rule-decision",
    label: "Rule decision view",
    example: "Determine ACCS eligibility from typed facts",
    semanticAdd: "Add rule, decision, or decision-table row",
    semanticConnect: "Add typed expression edge or rule binding",
  },
];

export function classifyEdit(view, action) {
  switch (action) {
    case "move":
      return { category: "layout", operation: "Update position only", semanticChanged: false };
    case "route":
      return { category: "layout", operation: "Update edge routing only", semanticChanged: false };
    case "hide":
      return { category: "view", operation: "Change inclusion/filter state only", semanticChanged: false };
    case "remove-from-view":
      return { category: "view", operation: "Exclude element from this view", semanticChanged: false };
    case "add":
      return { category: "semantic", operation: view.semanticAdd, semanticChanged: true };
    case "connect":
      return { category: "semantic", operation: view.semanticConnect, semanticChanged: true };
    case "delete-model":
      return { category: "semantic", operation: "Explicitly delete semantic concept after impact confirmation", semanticChanged: true };
    case "deployment-link":
      return view.id === "context-map"
        ? { category: "invalid", operation: "Deployment topology is not a bounded-context relationship", semanticChanged: false }
        : { category: "invalid", operation: "Operation is outside this view's declared vocabulary", semanticChanged: false };
    default:
      return { category: "invalid", operation: "Unknown or ambiguous gesture", semanticChanged: false };
  }
}
export function applyEdit(state, action) {
  const view = views[state.viewIndex];
  const result = classifyEdit(view, action);
  return {
    ...state,
    semanticRevision: state.semanticRevision + (result.category === "semantic" ? 1 : 0),
    viewRevision: state.viewRevision + (result.category === "view" ? 1 : 0),
    layoutRevision: state.layoutRevision + (result.category === "layout" ? 1 : 0),
    lastAction: action,
    lastResult: result,
  };
}
