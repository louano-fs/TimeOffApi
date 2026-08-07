export interface ManagerAssistantCapabilities {
  readonly enabled: boolean;
  readonly audience: string | null;
  readonly scope: string | null;
  readonly streaming: boolean;
  readonly maxMessageLength: number;
}

export type ManagerAssistantHistoryRole = 'user' | 'assistant';

export interface ManagerAssistantHistoryMessage {
  readonly role: ManagerAssistantHistoryRole;
  readonly text: string;
}

export interface ManagerAssistantMessageRequest {
  readonly message: string;
  readonly history?: readonly ManagerAssistantHistoryMessage[];
}

export interface TeamWorkedTimeEvidence {
  readonly employeeNumber: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly workedSeconds: number;
  readonly breakSeconds: number;
  readonly clockStatus: 'Working' | 'OnBreak' | 'ClockedOut';
  readonly rank: number | null;
}

export interface DirectReportCandidate {
  readonly displayName: string;
  readonly employeeNumber: string;
}

export interface TeamStatusEvidence {
  readonly employeeNumber: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly clockStatus: 'Working' | 'OnBreak' | 'ClockedOut';
}

export interface TeamWorkedTimeSummaryPart {
  readonly type: 'teamWorkedTimeSummary';
  readonly startDate: string;
  readonly endDate: string;
  readonly totalWorkedSeconds: number;
  readonly totalBreakSeconds: number;
  readonly averageWorkedSeconds: number | null;
  readonly includedMemberCount: number;
  readonly members: readonly TeamWorkedTimeEvidence[];
}

export interface TeamWorkedTimeThresholdPart {
  readonly type: 'teamWorkedTimeThreshold';
  readonly startDate: string;
  readonly endDate: string;
  readonly thresholdSeconds: number;
  readonly matchingMemberCount: number;
  readonly members: readonly TeamWorkedTimeEvidence[];
}

export interface DirectReportWorkedTimePart {
  readonly type: 'directReportWorkedTime';
  readonly startDate: string;
  readonly endDate: string;
  readonly member: TeamWorkedTimeEvidence;
}

export interface TeamCurrentStatusPart {
  readonly type: 'teamCurrentStatus';
  readonly includedMemberCount: number;
  readonly members: readonly TeamStatusEvidence[];
}

export interface TeamTimeLogExportPart {
  readonly type: 'teamTimeLogExport';
  readonly startDate: string;
  readonly endDate: string;
  readonly fileName: string;
  readonly downloadUrl: string;
}

export interface ScopeExplanationPart {
  readonly type: 'scopeExplanation';
  readonly destination: string;
}

export interface TeamMemberClarificationPart {
  readonly type: 'teamMemberClarification';
  readonly candidates: readonly DirectReportCandidate[];
}

export type ManagerAssistantResponsePart =
  | TeamWorkedTimeSummaryPart
  | TeamWorkedTimeThresholdPart
  | DirectReportWorkedTimePart
  | TeamCurrentStatusPart
  | TeamTimeLogExportPart
  | ScopeExplanationPart
  | TeamMemberClarificationPart;

export interface ManagerAssistantMessageResponse {
  readonly messageId: string;
  readonly answer: string;
  readonly asOf: string;
  readonly parts: readonly ManagerAssistantResponsePart[];
}

export interface ManagerAssistantUiMessage {
  readonly id: string;
  readonly role: ManagerAssistantHistoryRole;
  readonly text: string;
  readonly parts: readonly ManagerAssistantResponsePart[];
}
