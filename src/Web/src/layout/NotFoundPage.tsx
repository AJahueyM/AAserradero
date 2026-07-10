import { Button, Paper, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <Paper sx={{ p: 4 }}>
      <Stack spacing={2} sx={{ alignItems: 'flex-start' }}>
        <Typography component="h1" variant="h4">
          {t('routes.notFoundTitle')}
        </Typography>
        <Typography>{t('routes.notFoundBody')}</Typography>
        <Button component={RouterLink} to="/reservations" variant="contained">
          {t('routes.goHome')}
        </Button>
      </Stack>
    </Paper>
  );
}
