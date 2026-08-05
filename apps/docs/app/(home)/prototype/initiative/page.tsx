import { Suspense } from 'react';
import { InitiativeLandingPrototype } from './prototype';

// PROTOTYPE: Three ways to position Modeller as the path from business problem
// to deliberate intervention, switchable via ?variant=, on /prototype/initiative.
export default function InitiativePrototypePage() {
  return (
    <Suspense fallback={null}>
      <InitiativeLandingPrototype />
    </Suspense>
  );
}
