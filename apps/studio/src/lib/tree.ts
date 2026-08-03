export interface TreeFolder {
  kind: 'folder';
  name: string;
  path: string;
  children: TreeNode[];
}

export interface TreeDocument {
  kind: 'document';
  name: string;
  path: string;
}

export type TreeNode = TreeFolder | TreeDocument;

// Groups a flat, ordered `sources` list by real folder path — no filesystem
// walk, purely a function of the paths themselves.
export function buildTree(sources: string[]): TreeNode[] {
  const root: TreeFolder = { kind: 'folder', name: '', path: '', children: [] };

  for (const source of sources) {
    const segments = source.split('/');
    let current = root;
    for (let i = 0; i < segments.length - 1; i++) {
      const segment = segments[i];
      const folderPath = segments.slice(0, i + 1).join('/');
      let next = current.children.find((child): child is TreeFolder => child.kind === 'folder' && child.name === segment);
      if (!next) {
        next = { kind: 'folder', name: segment, path: folderPath, children: [] };
        current.children.push(next);
      }
      current = next;
    }
    current.children.push({ kind: 'document', name: segments[segments.length - 1], path: source });
  }

  return root.children;
}
