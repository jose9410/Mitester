// ─────────────────────────────────────────────────────────────────────────────
// MODELOS DEL GENERADOR DINÁMICO DE FORMULARIOS
// Representan los metadatos extraídos del schema-v1.1.json
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Tipos de widget que el schema-v1.1.json puede definir vía "ui:widget".
 * Cada tipo se mapea a un componente de Angular Material 3.
 */
export type UiWidget =
  | 'text'
  | 'textarea'
  | 'select'
  | 'datepicker'
  | 'tags'
  | 'autocomplete'
  | 'number'
  | 'checkbox';

/**
 * Tipo de acción de un step (polimorfismo del schema).
 * El DynamicFormGenerator usa esto para mostrar/ocultar campos condicionales.
 */
export type ActionType = 'UI_SEQUENCE' | 'SQL_EXECUTE' | 'ASSERT_BUSINESS_RULES' | string;

/**
 * Descriptor de campo dinámico generado por DynamicFormGeneratorService.
 * Cada instancia representa un control de formulario de Angular Material.
 */
export interface DynamicFieldConfig {
  /** Nombre del control en el FormGroup */
  key: string;
  /** Etiqueta visible en el formulario */
  label: string;
  /** Tipo de widget Material a renderizar */
  widget: UiWidget;
  /** ¿Es obligatorio? (basado en el array "required" del schema) */
  required: boolean;
  /** Valor por defecto del schema ("default") */
  defaultValue?: unknown;
  /** Opciones para widgets de tipo select/autocomplete (basadas en "enum") */
  options?: { value: string; label: string }[];
  /** Texto placeholder del schema ("ui:placeholder") */
  placeholder?: string;
  /** Descripción/hint del campo ("description") */
  hint?: string;
  /** Tipo de dato del schema (string, number, integer, boolean) */
  dataType: 'string' | 'number' | 'integer' | 'boolean' | 'array' | 'object';
  /** Validaciones adicionales (minLength, maxLength, min, max) */
  validators?: {
    minLength?: number;
    maxLength?: number;
    min?: number;
    max?: number;
    pattern?: string;
  };
}

/**
 * Configuración de un bloque de Step con sus campos polimórficos.
 * Generada por DynamicFormGeneratorService al parsear el array "steps".
 */
export interface StepFormConfig {
  stepIndex: number;
  /** Campos comunes de todos los steps (stepId, actionType, timeoutSeconds, retryCount) */
  commonFields: DynamicFieldConfig[];
  /**
   * Mapa de campos condicionales por actionType.
   * Se activa cuando el control "actionType" cambia de valor.
   */
  conditionalFields: Record<ActionType, DynamicFieldConfig[]>;
}

/**
 * Configuración completa de un proceso del schema.
 */
export interface ProcessFormConfig {
  processIndex: number;
  contextFields: DynamicFieldConfig[];
  steps: StepFormConfig[];
}

/**
 * Configuración raíz del formulario dinámico completo.
 */
export interface SuiteFormConfig {
  rootFields: DynamicFieldConfig[];    // tenant, environment, application, environmentRef
  processes: ProcessFormConfig[];
}
