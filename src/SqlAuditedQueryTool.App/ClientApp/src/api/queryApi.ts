// API client for SQL Audited Query Tool

export interface QueryResultColumn {
  name: string;
  type: string;
}

export interface QueryResultSet {
  columns: QueryResultColumn[];
  rows: Record<string, unknown>[];
  rowCount: number;
}

export interface QueryResult {
  succeeded?: boolean;
  errorMessage?: string | null;
  resultSets: QueryResultSet[];
  executionTimeMs: number;
  executionPlanXml?: string | null;
  // Legacy support for single result set
  columns?: QueryResultColumn[];
  rows?: Record<string, unknown>[];
  rowCount?: number;
}

export interface QuerySuggestion {
  sql: string;
  explanation: string;
  isFixQuery: boolean;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  suggestion?: QuerySuggestion;
}

export interface LlmResponse {
  message: string;
  suggestion?: QuerySuggestion;
  executedQuery?: string;
  executedResult?: QueryResult;
}

export interface SchemaColumn {
  columnName: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  defaultValue: string | null;
  isComputed: boolean;
}

export interface SchemaIndex {
  name: string;
  columns: string[];
  isUnique: boolean;
  isClustered: boolean;
}

export interface SchemaForeignKey {
  name: string;
  columns: string[];
  referencedSchema: string;
  referencedTable: string;
  referencedColumns: string[];
}

export interface SchemaTable {
  tableName: string;
  schemaName: string;
  columns: SchemaColumn[];
  primaryKey: string[];
  indexes: SchemaIndex[];
  foreignKeys: SchemaForeignKey[];
}

export interface SchemaContext {
  tables: SchemaTable[];
}

export interface ApiError {
  message: string;
  detail?: string;
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let errorMsg = `Request failed (${response.status})`;
    try {
      const body = await response.json() as ApiError;
      errorMsg = body.message || errorMsg;
    } catch {
      // use default message
    }
    throw new Error(errorMsg);
  }
  return response.json() as Promise<T>;
}

export async function executeQuery(sql: string, executionPlanMode: 'None' | 'Estimated' | 'Actual' = 'None'): Promise<QueryResult> {
  const response = await fetch('/api/query/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sql, executionPlanMode }),
  });
  const result = await handleResponse<QueryResult>(response);
  
  // Check if the query failed on the backend
  if (result.succeeded === false && result.errorMessage) {
    throw new Error(result.errorMessage);
  }
  
  return result;
}

export async function suggestQuery(message: string): Promise<QuerySuggestion> {
  const response = await fetch('/api/query/suggest', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message }),
  });
  return handleResponse<QuerySuggestion>(response);
}

export async function chat(
  message: string,
  history: ChatMessage[],
  timeoutMs = 180000,
): Promise<LlmResponse> {
  const messages = [...history.map((m) => ({ role: m.role, content: m.content })), { role: 'user', content: message }];
  
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
  
  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ messages, includeSchema: true }),
      signal: controller.signal,
    });
    clearTimeout(timeoutId);
    return handleResponse<LlmResponse>(response);
  } catch (err) {
    clearTimeout(timeoutId);
    if (err instanceof Error && err.name === 'AbortError') {
      throw new Error('Request timed out. The LLM is taking longer than expected. Please try again or simplify your question.');
    }
    throw err;
  }
}

export async function getSchema(): Promise<SchemaContext> {
  const response = await fetch('/api/schema');
  return handleResponse<SchemaContext>(response);
}
