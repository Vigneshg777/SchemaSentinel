export type RiskSeverity = 'Info' | 'Low' | 'Medium' | 'High' | 'Critical';

export type RiskCategory =
  | 'Destructive'
  | 'DataLoss'
  | 'Constraint'
  | 'Compatibility'
  | 'Dependency'
  | 'Performance'
  | 'Information';

export type AnalysisSource = 'StaticAnalysis' | 'DatabaseAnalysis';

export interface RiskFinding {
  id: string;
  ruleId: string;
  title: string;
  severity: RiskSeverity;
  category: RiskCategory;
  description: string;
  affectedObject?: string | null;
  evidence?: string | null;
  recommendation?: string | null;
  source: AnalysisSource;
  databaseChecked: boolean;
  databaseObjectFound: boolean;
}

export interface AffectedObject {
  name: string;
  objectType: string;
  existsInDatabase: boolean;
}

export interface DatabaseContext {
  databaseChecked: boolean;
  databaseName?: string | null;
  objectsFound: number;
  objectsNotFound: number;
}

export interface AnalysisResult {
  analysisId: string;
  overallSeverity: RiskSeverity;
  summary: string;
  findings: RiskFinding[];
  affectedObjects: AffectedObject[];
  databaseContext: DatabaseContext;
  recommendations: string[];
  createdAt: string;
  scriptName?: string | null;
}

export interface AnalysisHistoryItem {
  id: string;
  createdAt: string;
  scriptName: string;
  overallSeverity: RiskSeverity;
  summary: string;
}

export interface AnalyzeRequest {
  script: string;
  scriptName?: string | null;
}
