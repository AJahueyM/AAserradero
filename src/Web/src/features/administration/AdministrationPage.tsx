import { FeaturePlaceholder } from '../FeaturePlaceholder';

// Replaced by the administration feature agent (client admin + payment notes).
export default function AdministrationPage() {
  return (
    <FeaturePlaceholder
      labelKey="nav.administration"
      descriptionKey="routes.administrationDescription"
    />
  );
}
