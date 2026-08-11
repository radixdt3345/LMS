import { useEffect, useState, type SyntheticEvent } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress,
  Grid, Paper, Tab, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Tabs, Typography,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Tooltip, Legend } from 'chart.js';
import { Bar } from 'react-chartjs-2';
import { fetchHrDashboard } from '../../store/slices/dashboardSlice';
import { dashboardApi } from '../../api/dashboardApi';
import type { TrendsReportDto, UtilizationReportDto, ComplianceReportDto } from '../../api/dashboardApi';
import type { RootState, AppDispatch } from '../../store';

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip, Legend);

const STATUS_COLOUR_MAP: Record<string, 'success'|'warning'|'error'|'default'|'info'> = {
  Approved: 'success', Pending: 'warning', Rejected: 'error', Cancelled: 'default', Draft: 'info', Revoked: 'default',
};

function ComplianceGauge({ rate, withRequests, totalEmployees }: { rate: number; withRequests: number; totalEmployees: number }) {
  const colour = rate >= 80 ? 'success' : rate >= 50 ? 'warning' : 'error';
  return (
    <Box display="flex" flexDirection="column" alignItems="center" gap={2}>
      <Box position="relative" display="inline-flex">
        <CircularProgress variant="determinate" value={100} size={140} thickness={6} sx={{ color: 'grey.200', position: 'absolute', top: 0, left: 0 }} />
        <CircularProgress variant="determinate" value={Math.min(rate, 100)} size={140} thickness={6} color={colour} />
        <Box position="absolute" top={0} left={0} bottom={0} right={0} display="flex" alignItems="center" justifyContent="center">
          <Typography variant="h5" fontWeight={700}>{Math.round(rate)}%</Typography>
        </Box>
      </Box>
      <Typography variant="body2" color="text.secondary" textAlign="center">{withRequests} of {totalEmployees} employees submitted leave</Typography>
    </Box>
  );
}

export default function HrDashboardPage() {
  const dispatch = useDispatch<AppDispatch>();
  const { data, loading, error } = useSelector((state: RootState) => state.dashboard.hr);
  const [activeTab, setActiveTab] = useState(0);
  const [utilization, setUtilization] = useState<UtilizationReportDto | null>(null);
  const [utilizationLoading, setUtilizationLoading] = useState(false);
  const [utilizationError, setUtilizationError] = useState<string | null>(null);
  const [trends, setTrends] = useState<TrendsReportDto | null>(null);
  const [trendsLoading, setTrendsLoading] = useState(false);
  const [trendsError, setTrendsError] = useState<string | null>(null);
  const [compliance, setCompliance] = useState<ComplianceReportDto | null>(null);
  const [complianceLoading, setComplianceLoading] = useState(false);
  const [complianceError, setComplianceError] = useState<string | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  useEffect(() => { dispatch(fetchHrDashboard()); }, [dispatch]);

  useEffect(() => {
    if (activeTab === 1 && utilization === null && !utilizationLoading) {
      setUtilizationLoading(true);
      dashboardApi.getUtilization().then(setUtilization).catch(() => setUtilizationError('Failed to load utilization data.')).finally(() => setUtilizationLoading(false));
    }
    if (activeTab === 2 && trends === null && !trendsLoading) {
      setTrendsLoading(true);
      dashboardApi.getTrends().then(setTrends).catch(() => setTrendsError('Failed to load trends data.')).finally(() => setTrendsLoading(false));
    }
    if (activeTab === 3 && compliance === null && !complianceLoading) {
      setComplianceLoading(true);
      dashboardApi.getCompliance().then(setCompliance).catch(() => setComplianceError('Failed to load compliance data.')).finally(() => setComplianceLoading(false));
    }
  }, [activeTab, utilization, utilizationLoading, trends, trendsLoading, compliance, complianceLoading]);

  const handleExportCsv = async () => {
    setExportError(null);
    try {
      const response = await dashboardApi.exportCsv();
      const blob = new Blob([response.data as BlobPart], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `leave-report-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(anchor); anchor.click(); document.body.removeChild(anchor); URL.revokeObjectURL(url);
    } catch { setExportError('Export failed. Please try again.'); }
  };

  if (loading) return <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}><CircularProgress /></Box>;

  const utilRows = utilization?.rows ?? [];
  const trendRows = trends?.rows ?? [];
  const recentActivity = data?.recentActivity ?? [];

  return (
    <Box p={4}>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4">HR Dashboard</Typography>
        <Button variant="outlined" startIcon={<DownloadIcon />} onClick={() => { void handleExportCsv(); }}>Export CSV</Button>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {exportError && <Alert severity="error" sx={{ mb: 2 }}>{exportError}</Alert>}
      <Grid container spacing={3} mb={3}>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined"><CardContent sx={{ textAlign: 'center' }}>
            <Typography variant="h3" fontWeight={700} color="primary">{data?.totalEmployees ?? '—'}</Typography>
            <Typography variant="subtitle2" color="text.secondary" mt={0.5}>Total Employees</Typography>
          </CardContent></Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined"><CardContent sx={{ textAlign: 'center' }}>
            <Typography variant="h3" fontWeight={700} color="warning.main">{data?.pendingApprovals ?? '—'}</Typography>
            <Typography variant="subtitle2" color="text.secondary" mt={0.5}>Pending Approvals</Typography>
          </CardContent></Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined"><CardContent sx={{ textAlign: 'center' }}>
            <Typography variant="h3" fontWeight={700} color="info.main">{data?.activeLeaveToday ?? '—'}</Typography>
            <Typography variant="subtitle2" color="text.secondary" mt={0.5}>On Leave Today</Typography>
          </CardContent></Card>
        </Grid>
      </Grid>
      <Card variant="outlined">
        <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
          <Tabs value={activeTab} onChange={(_: SyntheticEvent, v: number) => setActiveTab(v)}>
            <Tab label="Recent Activity" /><Tab label="Utilization" /><Tab label="Trends" /><Tab label="Compliance" />
          </Tabs>
        </Box>
        {activeTab === 0 && (<CardContent>
          <Typography variant="h6" mb={2}>Recent Leave Activity</Typography>
          {recentActivity.length === 0 ? <Typography color="text.secondary">No recent activity.</Typography> : (
            <TableContainer component={Paper} elevation={0}><Table size="small">
              <TableHead><TableRow><TableCell>Leave Type</TableCell><TableCell>Start Date</TableCell><TableCell>End Date</TableCell><TableCell>Status</TableCell></TableRow></TableHead>
              <TableBody>{recentActivity.map(req => (<TableRow key={req.id}><TableCell>{req.leaveTypeName}</TableCell><TableCell>{req.startDate}</TableCell><TableCell>{req.endDate}</TableCell><TableCell><Chip label={req.status} color={STATUS_COLOUR_MAP[req.status] ?? 'default'} size="small" /></TableCell></TableRow>))}</TableBody>
            </Table></TableContainer>
          )}
        </CardContent>)}
        {activeTab === 1 && (<CardContent>
          <Typography variant="h6" mb={2}>Department Leave Utilization</Typography>
          {utilizationLoading && <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>}
          {utilizationError && <Alert severity="error">{utilizationError}</Alert>}
          {!utilizationLoading && utilRows.length > 0 && <Bar data={{ labels: utilRows.map(r => r.deptName), datasets: [{ label: 'Total Leave Days', data: utilRows.map(r => r.totalLeaveDays), backgroundColor: '#1976d2', borderRadius: 4 }] }} options={{ responsive: true, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } }} />}
        </CardContent>)}
        {activeTab === 2 && (<CardContent>
          <Typography variant="h6" mb={2}>Monthly Leave Trends</Typography>
          {trendsLoading && <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>}
          {trendsError && <Alert severity="error">{trendsError}</Alert>}
          {!trendsLoading && trendRows.length > 0 && <Bar data={{ labels: trendRows.map(r => r.yearMonth), datasets: [{ label: 'Approved', data: trendRows.map(r => r.approvedCount), backgroundColor: '#388e3c', borderRadius: 4 }, { label: 'Rejected', data: trendRows.map(r => r.rejectedCount), backgroundColor: '#d32f2f', borderRadius: 4 }] }} options={{ responsive: true, plugins: { legend: { position: 'top' } }, scales: { y: { beginAtZero: true } } }} />}
        </CardContent>)}
        {activeTab === 3 && (<CardContent>
          <Typography variant="h6" mb={3}>Leave Submission Compliance</Typography>
          {complianceLoading && <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>}
          {complianceError && <Alert severity="error">{complianceError}</Alert>}
          {!complianceLoading && compliance != null && <Box display="flex" justifyContent="center"><ComplianceGauge rate={compliance.submissionRatePercent} withRequests={compliance.employeesWithAtLeastOneRequest} totalEmployees={compliance.totalEmployees} /></Box>}
        </CardContent>)}
      </Card>
    </Box>
  );
}
