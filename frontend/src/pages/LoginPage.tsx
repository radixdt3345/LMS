import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDispatch, useSelector } from 'react-redux';
import { useForm, type SubmitHandler } from 'react-hook-form';
import { useMsal } from '@azure/msal-react';
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Divider,
  TextField,
  Typography,
  Alert,
} from '@mui/material';
import { loginRequest, clearError } from '../auth/authSlice';
import type { RootState, AppDispatch } from '../store';
import { loginRequest as msalLoginRequest } from '../auth/msalConfig';

interface LoginFormValues {
  email: string;
  password: string;
}

export default function LoginPage() {
  const navigate = useNavigate();
  const dispatch = useDispatch<AppDispatch>();
  const { instance } = useMsal();
  const { isAuthenticated, isLoading, error } = useSelector(
    (state: RootState) => state.auth,
  );

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({ mode: 'onBlur' });

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/dashboard', { replace: true });
    }
  }, [isAuthenticated, navigate]);

  useEffect(() => {
    dispatch(clearError());
  }, [dispatch]);

  const onSubmit: SubmitHandler<LoginFormValues> = (data) => {
    dispatch(loginRequest({ email: data.email, password: data.password }));
  };

  const handleMsalLogin = () => {
    instance.loginRedirect(msalLoginRequest).catch((err: unknown) => {
      console.error('[MSAL] loginRedirect error', err);
    });
  };

  return (
    <Box
      display="flex"
      alignItems="center"
      justifyContent="center"
      minHeight="100vh"
      bgcolor="grey.100"
    >
      <Card sx={{ width: '100%', maxWidth: 420, p: 2 }} elevation={3}>
        <CardContent>
          <Typography variant="h5" fontWeight={600} textAlign="center" mb={3}>
            Leave Management System
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }} role="alert">
              {error}
            </Alert>
          )}

          <Box
            component="form"
            onSubmit={handleSubmit(onSubmit)}
            noValidate
            data-testid="login-form"
          >
            <TextField
              label="Email address"
              type="email"
              fullWidth
              margin="normal"
              autoComplete="email"
              inputProps={{ 'data-testid': 'email-input' }}
              error={!!errors.email}
              helperText={errors.email?.message}
              {...register('email', {
                required: 'Email is required',
                pattern: {
                  value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                  message: 'Enter a valid email address',
                },
              })}
            />

            <TextField
              label="Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="current-password"
              inputProps={{ 'data-testid': 'password-input' }}
              error={!!errors.password}
              helperText={errors.password?.message}
              {...register('password', {
                required: 'Password is required',
                minLength: {
                  value: 6,
                  message: 'Password must be at least 6 characters',
                },
              })}
            />

            <Button
              type="submit"
              variant="contained"
              fullWidth
              size="large"
              disabled={isLoading}
              sx={{ mt: 2, mb: 1 }}
              data-testid="submit-button"
            >
              {isLoading ? <CircularProgress size={22} color="inherit" /> : 'Sign In'}
            </Button>
          </Box>

          <Divider sx={{ my: 2 }}>or</Divider>

          <Button
            variant="outlined"
            fullWidth
            size="large"
            onClick={handleMsalLogin}
            disabled={isLoading}
            data-testid="sso-button"
          >
            Sign in with Microsoft
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
