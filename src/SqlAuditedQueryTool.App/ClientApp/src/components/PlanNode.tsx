import { useState } from 'react';
import type { PlanNode as PlanNodeType } from '../utils/executionPlanParser';
import './PlanNode.css';

interface PlanNodeProps {
  node: PlanNodeType;
  level?: number;
}

export default function PlanNode({ node, level = 0 }: PlanNodeProps) {
  const [isExpanded, setIsExpanded] = useState(true); // Expand all by default
  const hasChildren = node.children.length > 0;

  const formatNumber = (num: number | undefined, decimals: number = 2): string => {
    if (num === undefined) return '-';
    return num.toFixed(decimals);
  };

  const getCostColor = (cost: number | undefined): string => {
    if (cost === undefined) return 'var(--color-text-secondary)';
    if (cost < 0.1) return 'var(--color-success, #4ade80)';
    if (cost < 1) return 'var(--color-warning, #fbbf24)';
    return 'var(--color-error, #f87171)';
  };

  const getOperationIcon = (opType: string): string => {
    const type = opType.toLowerCase();
    if (type.includes('scan')) return '🔍';
    if (type.includes('seek')) return '🎯';
    if (type.includes('sort')) return '🔀';
    if (type.includes('hash')) return '⚡';
    if (type.includes('merge')) return '🔗';
    if (type.includes('nested')) return '🔁';
    if (type.includes('filter')) return '⚗️';
    if (type.includes('aggregate')) return '∑';
    if (type.includes('compute')) return '🧮';
    if (type.includes('parallelism')) return '⚙️';
    if (type.includes('spool')) return '💾';
    if (type.includes('key lookup')) return '🔑';
    return '📋';
  };

  return (
    <div className="plan-node" style={{ marginLeft: level > 0 ? '24px' : '0' }}>
      <div className="plan-node-header">
        {hasChildren && (
          <button
            className="plan-node-toggle"
            onClick={() => setIsExpanded(!isExpanded)}
            aria-label={isExpanded ? 'Collapse' : 'Expand'}
          >
            {isExpanded ? '▼' : '▶'}
          </button>
        )}
        {!hasChildren && <span className="plan-node-spacer"></span>}
        
        <div className="plan-node-content">
          <div className="plan-node-main">
            <span className="plan-node-icon">{getOperationIcon(node.operationType)}</span>
            <span className="plan-node-type">{node.operationType}</span>
            {node.object && (
              <span className="plan-node-object">
                {node.object.schema && `${node.object.schema}.`}
                {node.object.table}
                {node.object.index && ` [${node.object.index}]`}
              </span>
            )}
          </div>
          
          <div className="plan-node-stats">
            <span className="plan-stat" style={{ color: getCostColor(node.estimateCost) }}>
              Cost: {formatNumber(node.estimateCost, 4)}
            </span>
            <span className="plan-stat">
              Est. Rows: {formatNumber(node.estimateRows, 0)}
            </span>
            {node.actualRows !== undefined && (
              <span className="plan-stat plan-stat-actual">
                Actual: {formatNumber(node.actualRows, 0)}
              </span>
            )}
          </div>

          {Object.keys(node.properties).length > 0 && (
            <div className="plan-node-properties">
              {Object.entries(node.properties).map(([key, value]) => (
                <div key={key} className="plan-property">
                  <span className="plan-property-key">{key}:</span>
                  <span className="plan-property-value">{value}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {isExpanded && hasChildren && (
        <div className="plan-node-children">
          {node.children.map((child, index) => (
            <PlanNode key={child.id || index} node={child} level={level + 1} />
          ))}
        </div>
      )}
    </div>
  );
}
