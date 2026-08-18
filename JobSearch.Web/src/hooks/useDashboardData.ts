import {
  fetchSummary,
  fetchApplications,
  fetchApplicationEvents,
  createApplication,
  updateApplicationStatus,
  fetchActivity,
  fetchHealth,
  fetchDiscoveries,
} from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useSummary() {
  return useAsyncData(fetchSummary, []);
}

export function useApplications(params: { status?: string; page?: number; pageSize?: number }) {
  return useAsyncData(() => fetchApplications(params), [params.status, params.page, params.pageSize]);
}

// Action, not auto-loading data — call execute(id) on demand (e.g. when a card expands),
// not on mount, since fetching every application's events up front would be wasteful.
export function useApplicationEvents() {
  return useAsyncAction(fetchApplicationEvents);
}

export function useCreateApplication() {
  return useAsyncAction(createApplication);
}

export function useUpdateApplicationStatus() {
  return useAsyncAction(updateApplicationStatus);
}

export function useActivity(limit = 30) {
  return useAsyncData(() => fetchActivity(limit), [limit]);
}

export function useHealth() {
  return useAsyncData(fetchHealth, []);
}

export function useDiscoveries(params: { recommendation?: string; page?: number; pageSize?: number }) {
  return useAsyncData(() => fetchDiscoveries(params), [params.recommendation, params.page, params.pageSize]);
}
