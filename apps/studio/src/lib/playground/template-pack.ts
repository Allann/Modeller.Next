// The C# domain-project template pack bundled into every playground workspace download (issue
// #73) — inlined from samples/ordering/templates/csharp/domain-project, same "ship it in the JS
// bundle, don't read local disk" approach as example-ordering.ts. Without this, the downloaded
// .modeller/config.json's `templatePack` field would point at a file that doesn't exist, and the
// download would not be the "complete Modeller workspace bundle" the issue asks for.
export const TEMPLATE_PACK_ROOT = 'templates/csharp/domain-project';

export const TEMPLATE_PACK_FILES: ReadonlyArray<{ path: string; content: string }> = [
  {
    path: `${TEMPLATE_PACK_ROOT}/pack.json`,
    content: `{
  "version": "1.0",
  "id": "csharp-domain-project",
  "packVersion": "1.0.0",
  "generationContractVersion": "1.0",
  "language": "csharp",
  "rendererId": "scriban",
  "rendererVersion": "1.0",
  "templates": [
    { "id": "solution", "path": "Solution.slnx.sbn", "digest": "sha256:83de235a04176e248fbc75cf2155b932ee4a4a2841529ec56a6b858370aec10a" },
    { "id": "project", "path": "DomainProject.csproj.sbn", "digest": "sha256:592070c8bc957c50d09f5b1bf70bfa1e63e76e56c948219f612a047a88eef48e" },
    { "id": "entity", "path": "Entity.cs.sbn", "digest": "sha256:8d78315565c4142feeac6a758b3d03c647a56075b6b3d1da35b39b3a08d88551" },
    { "id": "enumeration", "path": "Enumeration.cs.sbn", "digest": "sha256:da121a0ad1daada82766d7f6e5564a1f586370769b39bdb5fd74190e2f1c547e" },
    { "id": "rule", "path": "Rule.cs.sbn", "digest": "sha256:ce40a83420c470d5b6c1ee599ce10a92470836692c8f5007d0dbd001e4353595" },
    { "id": "behaviour", "path": "Behaviour.cs.sbn", "digest": "sha256:e2a501749fe99980133878c8575b69563d760494071a4a600aedfb46a38a53db" }
  ],
  "outputs": [
    { "id": "solution", "scope": "context", "templateId": "solution", "logicalPath": "{projectName}.slnx", "owner": "csharp-domain-project" },
    { "id": "project", "scope": "context", "templateId": "project", "logicalPath": "{projectName}/{projectName}.csproj", "owner": "csharp-domain-project" },
    { "id": "entity", "scope": "entity", "templateId": "entity", "logicalPath": "{projectName}/Entities/{definitionName}.cs", "owner": "csharp-domain-project" },
    { "id": "enumeration", "scope": "enumeration", "templateId": "enumeration", "logicalPath": "{projectName}/Enumerations/{definitionName}.cs", "owner": "csharp-domain-project" },
    { "id": "rule", "scope": "rule", "templateId": "rule", "logicalPath": "{projectName}/Rules/{definitionName}.cs", "owner": "csharp-domain-project" },
    { "id": "behaviour", "scope": "behaviour", "templateId": "behaviour", "logicalPath": "{projectName}/Behaviours/{definitionName}.cs", "owner": "csharp-domain-project" }
  ]
}
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/README.md`,
    content: `# C# domain-project pack

This reusable pack projects a complete canonical context into a compiling C#
domain project. Output recipes expand over every supported Entity, Enumeration,
Rule, and Behaviour; none names a Child Care definition.
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/DomainProject.csproj.sbn`,
    content: `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{{ target_framework }}</TargetFramework>
    <RootNamespace>{{ csharp_namespace }}</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/Solution.slnx.sbn`,
    content: `<Solution>
  <Project Path="{{ project_name }}/{{ project_name }}.csproj" />
</Solution>
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/Entity.cs.sbn`,
    content: `namespace {{ csharp_namespace }};

public sealed record {{ definition.name }}(
{{ for property in definition.properties }}    {{ property.type }}{{ if property.nullable }}?{{ end }} {{ property.name }}{{ if !for.last }},{{ end }}
{{ end }});
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/Enumeration.cs.sbn`,
    content: `namespace {{ csharp_namespace }};

public enum {{ definition.name }}
{
{{ for member in definition.members }}    {{ member.name }} = {{ member.value }},
{{ end }}}
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/Behaviour.cs.sbn`,
    content: `namespace {{ csharp_namespace }};

public enum {{ definition.stage_type }}
{
{{ for stage in definition.stages }}    {{ stage.name }}{{ if !for.last }},{{ end }}
{{ end }}}

public static class {{ definition.name }}
{
    public static {{ definition.stage_type }} Apply(
        {{ definition.stage_type }} current,
        {{ definition.facts_type }} facts) =>
{{ for t in definition.transitions }}        current == {{ definition.stage_type }}.{{ t.source_stage }} && {{ t.guard }}
            ? {{ definition.stage_type }}.{{ t.target_stage }}
            : {{ if for.last }}current;{{ else }}
{{ end }}{{ end }}
}
`,
  },
  {
    path: `${TEMPLATE_PACK_ROOT}/Rule.cs.sbn`,
    content: `namespace {{ csharp_namespace }};

public sealed record {{ definition.subject_name }}Facts(
{{ for fact in definition.facts }}    {{ fact.type }} {{ fact.name }}{{ if !for.last }},{{ end }}
{{ end }});

public static class {{ definition.subject_name }}
{
    public static bool Determine({{ definition.subject_name }}Facts facts) =>
{{ for term in definition.expression_terms }}        {{ term }}{{ if !for.last }} &&{{ else }};{{ end }}
{{ end }}}
`,
  },
];
