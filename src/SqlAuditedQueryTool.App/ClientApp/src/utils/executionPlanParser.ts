export interface PlanNode {
  id: string;
  operationType: string;
  logicalOp?: string;
  physicalOp?: string;
  estimateRows?: number;
  estimateCost?: number;
  estimateIO?: number;
  estimateCPU?: number;
  actualRows?: number;
  object?: {
    database?: string;
    schema?: string;
    table?: string;
    index?: string;
  };
  children: PlanNode[];
  properties: Record<string, string>;
}

export interface ParsedPlan {
  statementText?: string;
  totalCost?: number;
  rootNode: PlanNode;
}

function parseNumber(value: string | null | undefined): number | undefined {
  if (!value) return undefined;
  const num = parseFloat(value);
  return isNaN(num) ? undefined : num;
}

function parseRelOp(relOpElement: Element, nodeCounter: { value: number }): PlanNode {
  const node: PlanNode = {
    id: relOpElement.getAttribute('NodeId') || `node-${nodeCounter.value++}`,
    operationType: relOpElement.getAttribute('PhysicalOp') || 'Unknown',
    logicalOp: relOpElement.getAttribute('LogicalOp') || undefined,
    physicalOp: relOpElement.getAttribute('PhysicalOp') || undefined,
    estimateRows: parseNumber(relOpElement.getAttribute('EstimateRows')),
    estimateCost: parseNumber(relOpElement.getAttribute('EstimatedTotalSubtreeCost')),
    estimateIO: parseNumber(relOpElement.getAttribute('EstimateIO')),
    estimateCPU: parseNumber(relOpElement.getAttribute('EstimateCPU')),
    actualRows: parseNumber(relOpElement.getAttribute('ActualRows')),
    children: [],
    properties: {}
  };

  // Extract object information (table/index)
  const objectEl = relOpElement.querySelector('Object');
  if (objectEl) {
    node.object = {
      database: objectEl.getAttribute('Database') || undefined,
      schema: objectEl.getAttribute('Schema') || undefined,
      table: objectEl.getAttribute('Table') || undefined,
      index: objectEl.getAttribute('Index') || undefined,
    };
  }

  // Extract other properties
  const props: Record<string, string> = {};
  
  // Get Ordered attribute
  if (relOpElement.getAttribute('Ordered')) {
    props['Ordered'] = relOpElement.getAttribute('Ordered')!;
  }
  
  // Get Parallel attribute
  if (relOpElement.getAttribute('Parallel')) {
    props['Parallel'] = relOpElement.getAttribute('Parallel')!;
  }

  // Get predicate if exists
  const predicate = relOpElement.querySelector('Predicate > ScalarOperator');
  if (predicate?.textContent?.trim()) {
    props['Predicate'] = predicate.textContent.trim();
  }

  // Get output list
  const outputList = relOpElement.querySelector('OutputList');
  if (outputList) {
    const columns = Array.from(outputList.querySelectorAll('ColumnReference'))
      .map(col => col.getAttribute('Column'))
      .filter(Boolean);
    if (columns.length > 0) {
      props['Output'] = columns.join(', ');
    }
  }

  node.properties = props;

  // Parse child RelOp elements
  const childRelOps = Array.from(relOpElement.children).filter(
    child => child.tagName === 'RelOp'
  );
  
  node.children = childRelOps.map(child => parseRelOp(child as Element, nodeCounter));

  return node;
}

export function parseExecutionPlan(xmlString: string): ParsedPlan | null {
  try {
    const parser = new DOMParser();
    const xmlDoc = parser.parseFromString(xmlString, 'text/xml');

    // Check for parsing errors
    const parserError = xmlDoc.querySelector('parsererror');
    if (parserError) {
      console.error('XML parsing error:', parserError.textContent);
      return null;
    }

    // Find the statement
    const statement = xmlDoc.querySelector('StmtSimple');
    if (!statement) {
      console.error('No StmtSimple element found in execution plan');
      return null;
    }

    const statementText = statement.getAttribute('StatementText') || undefined;
    const totalCost = parseNumber(statement.getAttribute('StatementSubTreeCost'));

    // Find the root RelOp
    const queryPlan = statement.querySelector('QueryPlan');
    const rootRelOp = queryPlan?.querySelector('RelOp');
    
    if (!rootRelOp) {
      console.error('No RelOp element found in execution plan');
      return null;
    }

    const nodeCounter = { value: 0 };
    const rootNode = parseRelOp(rootRelOp, nodeCounter);

    return {
      statementText,
      totalCost,
      rootNode
    };
  } catch (error) {
    console.error('Failed to parse execution plan:', error);
    return null;
  }
}
