import { useState, useMemo } from 'react';
import { parseExecutionPlan } from '../utils/executionPlanParser';
import PlanNode from './PlanNode';
import './ExecutionPlanView.css';

interface ExecutionPlanViewProps {
  planXml: string;
}

export default function ExecutionPlanView({ planXml }: ExecutionPlanViewProps) {
  const [showXml, setShowXml] = useState(false);

  const parsedPlan = useMemo(() => {
    return parseExecutionPlan(planXml);
  }, [planXml]);

  const handleCopyXml = () => {
    navigator.clipboard.writeText(planXml).then(() => {
      alert('Execution plan XML copied to clipboard');
    }).catch((err) => {
      console.error('Failed to copy:', err);
      alert('Failed to copy to clipboard');
    });
  };

  const handleToggleView = () => {
    setShowXml(!showXml);
  };

  return (
    <div className="execution-plan-view">
      <div className="plan-toolbar">
        {parsedPlan && (
          <>
            {parsedPlan.totalCost !== undefined && (
              <span className="plan-total-cost">
                Total Cost: <strong>{parsedPlan.totalCost.toFixed(4)}</strong>
              </span>
            )}
            {parsedPlan.statementText && (
              <span className="plan-statement" title={parsedPlan.statementText}>
                {parsedPlan.statementText.length > 100 
                  ? parsedPlan.statementText.substring(0, 100) + '...' 
                  : parsedPlan.statementText}
              </span>
            )}
          </>
        )}
        <div style={{ marginLeft: 'auto', display: 'flex', gap: '8px' }}>
          <button className="plan-btn" onClick={handleToggleView} title={showXml ? 'Show visual plan' : 'Show XML'}>
            {showXml ? '📊 Visual' : '📄 XML'}
          </button>
          <button className="plan-btn" onClick={handleCopyXml} title="Copy XML to clipboard">
            📋 Copy XML
          </button>
        </div>
      </div>
      <div className="plan-content">
        {showXml ? (
          <pre className="plan-xml">{planXml}</pre>
        ) : parsedPlan ? (
          <div className="plan-visual">
            <PlanNode node={parsedPlan.rootNode} />
          </div>
        ) : (
          <div className="plan-error">
            Failed to parse execution plan. Please check the XML view.
          </div>
        )}
      </div>
    </div>
  );
}
