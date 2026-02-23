import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { getSchema } from '../api/queryApi';
import type { SchemaTable, SchemaColumn, SchemaIndex, SchemaForeignKey, SchemaContext } from '../api/queryApi';
import { useHorizontalResize } from '../hooks/useHorizontalResize';
import './SchemaTreeView.css';

interface SchemaTreeViewProps {
  onInsertText: (text: string) => void;
}

type OpenSet = Record<string, boolean>;

interface ContextMenuState {
  visible: boolean;
  x: number;
  y: number;
  table: SchemaTable | null;
}

function toggle(set: OpenSet, key: string): OpenSet {
  return { ...set, [key]: !set[key] };
}

function ChevronIcon({ open }: { open: boolean }) {
  return <span className="stv-chevron">{open ? '▾' : '▸'}</span>;
}

function ColumnNode({ col, depth, onInsert }: { col: SchemaColumn; depth: number; onInsert: (t: string) => void }) {
  const icon = col.isPrimaryKey ? '🔑' : '📝';
  return (
    <div
      className="stv-row stv-clickable"
      style={{ '--depth': depth } as React.CSSProperties}
      onClick={() => onInsert(col.columnName)}
      title={`Click to insert: ${col.columnName}`}
    >
      <span className="stv-chevron" />
      <span className="stv-icon">{icon}</span>
      <span className="stv-label">{col.columnName}</span>
      <span className="stv-type">({col.dataType}{col.isNullable ? ', null' : ''})</span>
      {col.isPrimaryKey && <span className="stv-badge stv-badge--pk">PK</span>}
      {col.isIdentity && <span className="stv-badge stv-badge--identity">ID</span>}
      {col.isComputed && <span className="stv-badge stv-badge--computed">calc</span>}
      {col.defaultValue != null && <span className="stv-default">= {col.defaultValue}</span>}
    </div>
  );
}

function IndexNode({ idx, depth }: { idx: SchemaIndex; depth: number }) {
  return (
    <div className="stv-row" style={{ '--depth': depth } as React.CSSProperties} title={idx.name}>
      <span className="stv-chevron" />
      <span className="stv-icon">📇</span>
      <span className="stv-label">{idx.name}</span>
      <span className="stv-type">({idx.columns.join(', ')})</span>
      {idx.isUnique && <span className="stv-badge stv-badge--unique">UQ</span>}
      {idx.isClustered && <span className="stv-badge stv-badge--clustered">CL</span>}
    </div>
  );
}

function ForeignKeyNode({ fk, depth }: { fk: SchemaForeignKey; depth: number }) {
  const ref = `${fk.referencedSchema !== 'dbo' ? fk.referencedSchema + '.' : ''}${fk.referencedTable}(${fk.referencedColumns.join(', ')})`;
  return (
    <div className="stv-row" style={{ '--depth': depth } as React.CSSProperties} title={fk.name}>
      <span className="stv-chevron" />
      <span className="stv-icon">🔗</span>
      <span className="stv-label">{fk.name}</span>
      <span className="stv-fk-ref">→ {ref}</span>
    </div>
  );
}

function SectionNode({ label, icon, children, sectionKey, open, onToggle, depth, emptyText }: {
  label: string; icon: string; children: React.ReactNode[]; sectionKey: string;
  open: boolean; onToggle: (key: string) => void; depth: number; emptyText: string;
}) {
  return (
    <div className="stv-node">
      <div className="stv-row" style={{ '--depth': depth } as React.CSSProperties} onClick={() => onToggle(sectionKey)}>
        <ChevronIcon open={open} />
        <span className="stv-icon">{icon}</span>
        <span className="stv-label">{label}</span>
        <span className="stv-type">({children.length})</span>
      </div>
      {open && (children.length > 0
        ? children
        : <div className="stv-empty" style={{ '--depth': depth + 1 } as React.CSSProperties}>{emptyText}</div>
      )}
    </div>
  );
}

function TableNode({ table, openSet, onToggle, onInsert, onContextMenu }: {
  table: SchemaTable; openSet: OpenSet; onToggle: (key: string) => void; onInsert: (text: string) => void;
  onContextMenu: (e: React.MouseEvent, table: SchemaTable) => void;
}) {
  const tKey = `${table.schemaName}.${table.tableName}`;
  const isOpen = openSet[tKey] ?? false;
  const qualifiedName = table.schemaName === 'dbo' ? table.tableName : `[${table.schemaName}].[${table.tableName}]`;

  return (
    <div className="stv-node">
      <div 
        className="stv-row" 
        style={{ '--depth': 1 } as React.CSSProperties}
        onContextMenu={(e) => onContextMenu(e, table)}
      >
        <span onClick={() => onToggle(tKey)} style={{ display: 'contents' }}>
          <ChevronIcon open={isOpen} />
          <span className="stv-icon">📋</span>
        </span>
        <span
          className="stv-label stv-clickable"
          onClick={() => onInsert(qualifiedName)}
          title={`Click to insert: ${qualifiedName}`}
          style={{ cursor: 'pointer' }}
        >
          <span className="stv-label">{table.tableName}</span>
        </span>
      </div>
      {isOpen && (
        <>
          <SectionNode
            label="Columns" icon="📂" sectionKey={`${tKey}.cols`}
            open={openSet[`${tKey}.cols`] ?? true} onToggle={onToggle}
            depth={2} emptyText="(none)"
          >
            {table.columns.map((col) => (
              <ColumnNode key={col.columnName} col={col} depth={3} onInsert={onInsert} />
            ))}
          </SectionNode>

          <SectionNode
            label="Indexes" icon="📂" sectionKey={`${tKey}.idx`}
            open={openSet[`${tKey}.idx`] ?? false} onToggle={onToggle}
            depth={2} emptyText="(none)"
          >
            {(table.indexes ?? []).map((idx) => (
              <IndexNode key={idx.name} idx={idx} depth={3} />
            ))}
          </SectionNode>

          <SectionNode
            label="Foreign Keys" icon="📂" sectionKey={`${tKey}.fk`}
            open={openSet[`${tKey}.fk`] ?? false} onToggle={onToggle}
            depth={2} emptyText="(none)"
          >
            {(table.foreignKeys ?? []).map((fk) => (
              <ForeignKeyNode key={fk.name} fk={fk} depth={3} />
            ))}
          </SectionNode>
        </>
      )}
    </div>
  );
}

export default function SchemaTreeView({ onInsertText }: SchemaTreeViewProps) {
  const [schema, setSchema] = useState<SchemaContext | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [openSet, setOpenSet] = useState<OpenSet>({});
  const [collapsed, setCollapsed] = useState(false);
  const [contextMenu, setContextMenu] = useState<ContextMenuState>({ visible: false, x: 0, y: 0, table: null });
  const contextMenuRef = useRef<HTMLDivElement>(null);
  
  const { width, handleMouseDown } = useHorizontalResize({
    initialWidth: 280,
    minWidth: 220,
    maxWidth: 600,
    storageKey: 'schemaTreeWidth',
  });

  const loadSchema = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getSchema();
      setSchema(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load schema');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadSchema(); }, [loadSchema]);

  const handleToggle = useCallback((key: string) => {
    setOpenSet((prev) => toggle(prev, key));
  }, []);

  const handleContextMenu = useCallback((e: React.MouseEvent, table: SchemaTable) => {
    e.preventDefault();
    e.stopPropagation();
    setContextMenu({ visible: true, x: e.clientX, y: e.clientY, table });
  }, []);

  const closeContextMenu = useCallback(() => {
    setContextMenu({ visible: false, x: 0, y: 0, table: null });
  }, []);

  const handleContextMenuSelect = useCallback((query: string) => {
    onInsertText(query);
    closeContextMenu();
  }, [onInsertText, closeContextMenu]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (contextMenuRef.current && !contextMenuRef.current.contains(e.target as Node)) {
        closeContextMenu();
      }
    };
    
    if (contextMenu.visible) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [contextMenu.visible, closeContextMenu]);

  // Group tables by schema, filter by name
  const grouped = useMemo(() => {
    if (!schema) return {};
    const lf = filter.toLowerCase();
    const tables = lf
      ? schema.tables.filter((t) => t.tableName.toLowerCase().includes(lf))
      : schema.tables;
    const map: Record<string, SchemaTable[]> = {};
    for (const t of tables) {
      (map[t.schemaName] ??= []).push(t);
    }
    return map;
  }, [schema, filter]);

  const schemaNames = useMemo(() => Object.keys(grouped).sort(), [grouped]);

  return (
    <div className={`stv${collapsed ? ' stv--collapsed' : ''}`} style={{ width: collapsed ? undefined : `${width}px` }}>
      <div className="stv-resize-handle" onMouseDown={handleMouseDown} />
      <div className="stv-header">
        <span className="stv-header-title">Schema</span>
        <button className="stv-btn-refresh" onClick={loadSchema} title="Refresh schema">🔄</button>
        <button className="stv-btn-collapse" onClick={() => setCollapsed((v) => !v)} title={collapsed ? 'Expand' : 'Collapse'}>
          {collapsed ? '▶' : '◀'}
        </button>
      </div>

      <div className="stv-filter">
        <input
          type="text"
          placeholder="🔍 Filter tables…"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
      </div>

      {loading && <div className="stv-loading">Loading schema…</div>}

      {error && (
        <div className="stv-error">
          <span>{error}</span>
          <button onClick={loadSchema}>Retry</button>
        </div>
      )}

      {!loading && !error && (
        <div className="stv-tree">
          {schemaNames.length === 0 && (
            <div className="stv-empty" style={{ '--depth': 0 } as React.CSSProperties}>
              {filter ? 'No matching tables' : 'No tables found'}
            </div>
          )}
          {schemaNames.map((schemaName) => {
            const sKey = `schema:${schemaName}`;
            const isOpen = openSet[sKey] ?? true;
            return (
              <div key={schemaName} className="stv-node">
                <div className="stv-row" style={{ '--depth': 0 } as React.CSSProperties} onClick={() => handleToggle(sKey)}>
                  <ChevronIcon open={isOpen} />
                  <span className="stv-icon">📁</span>
                  <span className="stv-label">{schemaName}</span>
                  <span className="stv-type">({grouped[schemaName].length})</span>
                </div>
                {isOpen && grouped[schemaName].map((table) => (
                  <TableNode
                    key={`${table.schemaName}.${table.tableName}`}
                    table={table}
                    openSet={openSet}
                    onToggle={handleToggle}
                    onInsert={onInsertText}
                    onContextMenu={handleContextMenu}
                  />
                ))}
              </div>
            );
          })}
        </div>
      )}

      {contextMenu.visible && contextMenu.table && (
        <div 
          ref={contextMenuRef}
          className="stv-context-menu"
          style={{ 
            left: `${contextMenu.x}px`, 
            top: `${contextMenu.y}px` 
          }}
        >
          <div 
            className="stv-context-menu-item"
            onClick={() => {
              const qualifiedName = contextMenu.table!.schemaName === 'dbo' 
                ? contextMenu.table!.tableName 
                : `[${contextMenu.table!.schemaName}].[${contextMenu.table!.tableName}]`;
              handleContextMenuSelect(`SELECT TOP 1000 * FROM ${qualifiedName}`);
            }}
          >
            SELECT TOP 1000 *
          </div>
          <div 
            className="stv-context-menu-item"
            onClick={() => {
              const qualifiedName = contextMenu.table!.schemaName === 'dbo' 
                ? contextMenu.table!.tableName 
                : `[${contextMenu.table!.schemaName}].[${contextMenu.table!.tableName}]`;
              handleContextMenuSelect(`SELECT COUNT(*) FROM ${qualifiedName}`);
            }}
          >
            SELECT COUNT(*)
          </div>
          <div 
            className="stv-context-menu-item"
            onClick={() => {
              const qualifiedName = contextMenu.table!.schemaName === 'dbo' 
                ? contextMenu.table!.tableName 
                : `[${contextMenu.table!.schemaName}].[${contextMenu.table!.tableName}]`;
              handleContextMenuSelect(`SELECT * FROM ${qualifiedName} WHERE `);
            }}
          >
            SELECT * WHERE...
          </div>
        </div>
      )}
    </div>
  );
}
