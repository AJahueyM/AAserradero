import { Paper, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

interface FeaturePlaceholderProps {
  labelKey: string;
  descriptionKey: string;
}
export function FeaturePlaceholder({ labelKey, descriptionKey }: FeaturePlaceholderProps) {
  const { t } = useTranslation();
  const section = t(labelKey);
  return (
    <Paper sx={{ p: { xs: 3, md: 4 } }}>
      <Stack spacing={2}>
        <Typography component="h1" variant="h4">
          {t('routes.placeholderTitle', { section })}
        </Typography>
        <Typography color="text.secondary">{t(descriptionKey)}</Typography>
        <Typography>{t('routes.placeholderBody')}</Typography>
      </Stack>
    </Paper>
  );
}
