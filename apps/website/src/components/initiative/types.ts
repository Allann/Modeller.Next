/** Runs a mutating API call, then refetches the session on success (or surfaces the error) —
 * shared by every Facilitator cockpit section so each one stays focused on its own UI. */
export type RunAction = (action: () => Promise<unknown>) => Promise<void>;
