import { Paper, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

export function RegulationPage() {
  const { t } = useTranslation();
  return (
    <Paper sx={{ p: { xs: 3, md: 4 } }}>
      <Stack spacing={2}>
        <Typography component="h1" variant="h4">
          {t('regulation.title')}
        </Typography>
        <Typography>{t('regulation.intro')}</Typography>
        <Typography color="text.secondary">{t('regulation.availability')}</Typography>
      </Stack>
    </Paper>
  );
}
