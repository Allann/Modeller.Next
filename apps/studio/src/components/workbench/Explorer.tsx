'use client';

import type { TreeNode } from '@/lib/tree';

export function Explorer({
  nodes,
  activePath,
  onOpenDocument,
  depth = 0,
}: {
  nodes: TreeNode[];
  activePath: string | undefined;
  onOpenDocument: (path: string) => void;
  depth?: number;
}) {
  return (
    <>
      {nodes.map((node) =>
        node.kind === 'folder' ? (
          <div key={node.path || node.name}>
            <div className="explorer-folder" style={{ ['--depth' as string]: depth }}>
              {node.name}
            </div>
            <Explorer nodes={node.children} activePath={activePath} onOpenDocument={onOpenDocument} depth={depth + 1} />
          </div>
        ) : (
          <div
            key={node.path}
            className={`explorer-doc${node.path === activePath ? ' active' : ''}`}
            style={{ ['--depth' as string]: depth }}
            onClick={() => onOpenDocument(node.path)}
          >
            {node.name}
          </div>
        ),
      )}
    </>
  );
}
