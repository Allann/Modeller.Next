import type { InitiativeField } from './initiativeTypes';

export type InitiativeFieldGuidance = {
  /** What this field records, in one or two sentences a first-time Facilitator can act on. */
  captures: string;
  /** A question the Facilitator can send as-is or adapt. */
  exampleQuestion: string;
};

/**
 * Facilitator guidance for each Discover/Frame field. The wording and example questions follow
 * docs/concepts/initiatives.mdx and the worked example in docs/guides/building-variation-initiative.md,
 * so the cockpit and the docs teach the same thing — change them together.
 */
export const INITIATIVE_FIELD_GUIDANCE: Record<InitiativeField, InitiativeFieldGuidance> = {
  ProblemStatement: {
    captures:
      'The underlying problem behind the original request. Treat the requested solution as a proposed answer, and ask what is actually going wrong.',
    exampleQuestion: 'What problem would the requested change solve?',
  },
  PainPoints: {
    captures: 'What happens today and how often it causes trouble. Ask for concrete examples, numbers, and evidence.',
    exampleQuestion: 'What happens today, and how often does it cause a problem?',
  },
  AffectedUsers: {
    captures: 'The people and groups affected by the problem, including who feels it first and who finds out later.',
    exampleQuestion: 'Who is affected when this goes wrong?',
  },
  Outcomes: {
    captures: 'What would be observably different if the problem were solved. Describe results, not features.',
    exampleQuestion: 'What would be observably different if the problem were solved?',
  },
  SuccessCriteria: {
    captures: 'How you will decide whether the change worked. Prefer measurable targets, such as a time limit or a percentage.',
    exampleQuestion: 'How would you decide whether the change was successful?',
  },
  NonGoals: {
    captures: 'What is explicitly outside the scope of this Initiative, so that the work stays bounded.',
    exampleQuestion: 'What is explicitly outside the scope of this Initiative?',
  },
  Constraints: {
    captures: 'Limits that any intervention must respect, such as systems that must stay, rules, budget, time, or skills.',
    exampleQuestion: 'What constraints must any intervention respect?',
  },
  Assumptions: {
    captures: 'What the team believes but has not yet proved. Making assumptions visible lets you test them before you commit.',
    exampleQuestion: 'What are we assuming but have not yet proved?',
  },
  OpenQuestions: {
    captures: 'What still needs an answer before you select or design a change, and who could answer it.',
    exampleQuestion: 'What still needs an answer before selecting or designing a change?',
  },
  Risks: {
    captures: 'What could go wrong if you make a change, and also if you do nothing.',
    exampleQuestion: 'What could go wrong if we change this, or if we do nothing?',
  },
};
