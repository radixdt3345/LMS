import axios, { type AxiosError } from 'axios';

const API_BASE = import.meta.env.VITE_API_BASE_URL as string;

export interface HolidayDto {
  id: string;
  date: string;    // ISO date string e.g. "2025-01-01"
  name: string;
  isOptional: boolean;
  year: number;
}

export interface CreateHolidayPayload {
  date: string;
  name: string;
  isOptional: boolean;
}

export interface BulkImportError {
  row: number;
  error: string;
}

export interface BulkImportResult {
  imported: number;
  errors: BulkImportError[];
}

function authHeader(token: string) {
  return { Authorization: `Bearer ${token}` };
}

export async function fetchHolidays(
  token: string,
  year: number,
): Promise<HolidayDto[]> {
  const res = await axios.get<{ success: boolean; data: { items: HolidayDto[] } }>(
    `${API_BASE}/api/v1/holidays`,
    { headers: authHeader(token), params: { year } },
  );
  return res.data.data.items;
}

export async function createHoliday(
  token: string,
  payload: CreateHolidayPayload,
): Promise<HolidayDto> {
  const res = await axios.post<{ success: boolean; data: HolidayDto }>(
    `${API_BASE}/api/v1/holidays`,
    payload,
    { headers: authHeader(token) },
  );
  return res.data.data;
}

export async function deleteHoliday(token: string, id: string): Promise<void> {
  await axios.delete(`${API_BASE}/api/v1/holidays/${id}`, {
    headers: authHeader(token),
  });
}

export async function bulkImportHolidays(
  token: string,
  file: File,
): Promise<BulkImportResult> {
  const formData = new FormData();
  formData.append('file', file);
  const res = await axios.post<{ success: boolean; data: BulkImportResult }>(
    `${API_BASE}/api/v1/holidays/bulk-import`,
    formData,
    {
      headers: {
        ...authHeader(token),
        'Content-Type': 'multipart/form-data',
      },
    },
  );
  return res.data.data;
}
