export interface Summary {
  total: number;
  classified: number;
  jobRelated: number;
  byCategory: Record<string, number>;
}

export interface EmailItem {
  messageId: string;
  from: string;
  subject: string;
  receivedAt: string;
  isJobRelated: boolean;
  category: string | null;
  company: string | null;
  roleTitle: string | null;
  confidence: number | null;
}

export interface EmailsResponse {
  items: EmailItem[];
  total: number;
  page: number;
  pageSize: number;
}
