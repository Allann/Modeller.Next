import { ALL_FIELDS, INITIATIVE_FIELD_LABELS, type InitiativeField } from '@/lib/initiativeTypes';

export function StructuredFieldsSection({ structuredFields }: { structuredFields: Record<InitiativeField, string[]> }) {
  const populated = ALL_FIELDS.filter((f) => structuredFields[f].length > 0);
  if (populated.length === 0) return null;

  return (
    <section aria-label="Structured fields">
      <h2>Structured record</h2>
      {populated.map((f) => (
        <div key={f}>
          <p className="panel-kicker">{INITIATIVE_FIELD_LABELS[f]}</p>
          <ul className="item-list">
            {structuredFields[f].map((text, index) => (
              <li key={index}>{text}</li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}
