import React, { useEffect } from 'react'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  Divider,
  TextField,
  Typography,
} from '@mui/material'
import { useForm } from 'react-hook-form'
import { useMsal } from '@azure/msal-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'

interface LoginFormValues {
  email: string
  password: string
}

function LoginPage(): JSX.Element {
  const { login, isAuthenticated, isLoading, error, clearError } = useAuth()
  const { instance } = useMsal()
  const navigate = useNavigate()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({ mode: 'onBlur' })

  // Redirect to dashboard after successful login
  useEffect(() => {
    if (isAuthenticated) {
      navigate('/', { replace: true })
    }
  }, [isAuthenticated, navigate])

  const onSubmit = (values: LoginFormValues): void => {
    clearError()
    login({ email: values.email, password: values.password })
  }

  const handleSsoLogin = async (): Promise<void> => {
    try {
      await instance.loginRedirect({
        scopes: ['openid', 'profile', 'email'],
      })
    } catch (err) {
      // loginRedirect navigates away; errors here are edge cases (popup blocked, etc.)
      console.error('SSO redirect failed', err)
    }
  }

  return (
    <Container maxWidth="xs">
      <Box
        sx={{
          mt: 8,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 2,
        }}
      >
        <Typography component="h1" variant="h5">
          Sign in to LMS
        </Typography>

        {error != null && (
          <Alert severity="error" sx={{ width: '100%' }}>
            {error}
          </Alert>
        )}

        <Box
          component="form"
          onSubmit={handleSubmit(onSubmit)}
          sx={{ width: '100%', mt: 1 }}
          noValidate
        >
          <TextField
            label="Email"
            type="email"
            fullWidth
            margin="normal"
            autoComplete="email"
            autoFocus
            {...register('email', {
              required: 'Email is required',
              pattern: {
                value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                message: 'Enter a valid email address',
              },
            })}
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
          />

          <TextField
            label="Password"
            type="password"
            fullWidth
            margin="normal"
            autoComplete="current-password"
            {...register('password', {
              required: 'Password is required',
              minLength: {
                value: 8,
                message: 'Password must be at least 8 characters',
              },
            })}
            error={Boolean(errors.password)}
            helperText={errors.password?.message}
          />

          <Button
            type="submit"
            fullWidth
            variant="contained"
            sx={{ mt: 2, mb: 1 }}
            disabled={isLoading}
            startIcon={
              isLoading ? <CircularProgress size={18} color="inherit" /> : null
            }
          >
            {isLoading ? 'Signing in...' : 'Sign in'}
          </Button>
        </Box>

        <Divider sx={{ width: '100%' }}>OR</Divider>

        <Button
          fullWidth
          variant="outlined"
          onClick={() => { void handleSsoLogin() }}
          disabled={isLoading}
        >
          Sign in with Microsoft
        </Button>
      </Box>
    </Container>
  )
}

export default LoginPage
