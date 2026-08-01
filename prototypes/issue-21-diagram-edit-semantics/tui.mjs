import readline from "node:readline";
import { applyEdit, views } from "./classifier.mjs";

let state = {
  viewIndex: 0,
  semanticRevision: 0,
  viewRevision: 0,
  layoutRevision: 0,
  lastAction: "none",
  lastResult: { category: "none", operation: "No edit applied", semanticChanged: false },
};

const keys = {
  m: "move",
  r: "route",
  h: "hide",
  v: "remove-from-view",
  a: "add",
  c: "connect",
  d: "delete-model",
  p: "deployment-link",
};

function render() {
  const view = views[state.viewIndex];
  console.clear();
  console.log("\x1b[1mPROTOTYPE — diagram edit semantics\x1b[0m");
  console.log(`\x1b[1mView:\x1b[0m ${view.label}`);
  console.log(`\x1b[1mChild Care example:\x1b[0m ${view.example}`);
  console.log(`\x1b[1mSemantic revision:\x1b[0m ${state.semanticRevision}`);
  console.log(`\x1b[1mView revision:\x1b[0m ${state.viewRevision}`);
  console.log(`\x1b[1mLayout revision:\x1b[0m ${state.layoutRevision}`);
  console.log(`\x1b[1mLast edit:\x1b[0m ${state.lastAction}`);
  console.log(`\x1b[1mClassification:\x1b[0m ${state.lastResult.category}`);
  console.log(`\x1b[1mOperation:\x1b[0m ${state.lastResult.operation}`);
  console.log("\n\x1b[2m[n] next view  [b] previous view  [m] move  [r] route edge");
  console.log("[h] hide/show  [v] remove from view  [a] add concept  [c] connect");
  console.log("[d] delete from model  [p] deployment link  [q] quit\x1b[0m");
}

readline.emitKeypressEvents(process.stdin);
if (process.stdin.isTTY) process.stdin.setRawMode(true);
render();

process.stdin.on("keypress", (_text, key) => {
  if (key.name === "q" || (key.ctrl && key.name === "c")) process.exit(0);
  if (key.name === "n") state = { ...state, viewIndex: (state.viewIndex + 1) % views.length };
  else if (key.name === "b") state = { ...state, viewIndex: (state.viewIndex - 1 + views.length) % views.length };
  else if (keys[key.name]) state = applyEdit(state, keys[key.name]);
  render();
});
