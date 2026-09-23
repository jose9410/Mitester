import { Injectable } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ValidatorFn,
  AbstractControl,
} from '@angular/forms';
import {
  DynamicFieldConfig,
  StepFormConfig,
  ProcessFormConfig,
  SuiteFormConfig,
  UiWidget,
  ActionType,
} from '../models/suite-schema.models';

// ─────────────────────────────────────────────────────────────────────────────
// DynamicFormGeneratorService
// ─────────────────────────────────────────────────────────────────────────────
// Parsea el schema-v1.1.json (JSON Schema draft-07) y genera:
//   1. SuiteFormConfig: Metadata descriptiva para renderizar los widgets en la plantilla
//   2. FormGroup (Reactive Forms): El modelo de datos reactivo de Angular
//
// Flujo:
//   schema-v1.1.json → parseSchema() → SuiteFormConfig
//                    → buildFormGroup() → FormGroup (para el template)
//
// El mapeo ui:widget → Angular Material:
//   "text"        → mat-form-field + matInput
//   "textarea"    → mat-form-field + textarea matInput
//   "select"      → mat-select + mat-option (opciones del "enum")
//   "datepicker"  → mat-datepicker
//   "autocomplete"→ mat-autocomplete
//   "tags"        → mat-chip-listbox
//   (default)     → mat-form-field + matInput
// ─────────────────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class DynamicFormGeneratorService {

  private readonly fb = new FormBuilder();

  // ── MÉTODOS PÚBLICOS ──────────────────────────────────────────────────────

  /**
   * Parsea el JSON Schema completo y genera la configuración de metadata
   * que la plantilla usará para renderizar los controles de Material.
   */
  parseSchema(schema: Record<string, unknown>): SuiteFormConfig {
    const properties = (schema['properties'] as Record<string, unknown>) ?? {};
    const required = (schema['required'] as string[]) ?? [];

    // Campos raíz: tenant, environment, application, environmentRef
    const rootFields = this.extractFieldsFromProperties(properties, required, [
      'processes',
    ]);

    // Procesos — en este schema es un array de templates, no instancias reales
    // Generamos una plantilla de proceso vacía para el formulario "añadir proceso"
    const processesSchema = (properties['processes'] as Record<string, unknown>) ?? {};
    const itemSchema = (processesSchema['items'] as Record<string, unknown>) ?? {};

    const processTemplate = this.buildProcessFormConfig(itemSchema, 0);

    return {
      rootFields,
      processes: [processTemplate],
    };
  }

  /**
   * Construye el FormGroup reactivo raíz a partir de la SuiteFormConfig.
   * El FormGroup generado es compatible con los controles de Material.
   */
  buildFormGroup(config: SuiteFormConfig): FormGroup {
    const rootControls: Record<string, unknown> = {};

    // Campos raíz
    for (const field of config.rootFields) {
      rootControls[field.key] = [
        field.defaultValue ?? '',
        this.buildValidators(field),
      ];
    }

    // Array de procesos
    rootControls['processes'] = this.fb.array(
      config.processes.map((p) => this.buildProcessFormGroup(p)),
    );

    return this.fb.group(rootControls);
  }

  /**
   * Agrega un nuevo proceso vacío a un FormArray de procesos existente.
   * Útil para el botón "+ Agregar Proceso" en la UI.
   */
  addProcessToArray(
    processArray: FormArray,
    processConfig: ProcessFormConfig,
  ): void {
    processArray.push(this.buildProcessFormGroup(processConfig));
  }

  /**
   * Agrega un nuevo step vacío al FormArray de steps de un proceso.
   */
  addStepToProcess(
    stepsArray: FormArray,
    stepConfig: StepFormConfig,
  ): void {
    stepsArray.push(this.buildStepFormGroup(stepConfig));
  }

  /**
   * Serializa el FormGroup a JSON string listo para enviar al backend
   * como suiteConfigJson en el StartExecutionRequest.
   */
  serializeToSuiteJson(formValue: Record<string, unknown>): string {
    return JSON.stringify(formValue, null, 2);
  }

  // ── PARSEO DEL SCHEMA ─────────────────────────────────────────────────────

  private extractFieldsFromProperties(
    properties: Record<string, unknown>,
    required: string[],
    excludeKeys: string[] = [],
  ): DynamicFieldConfig[] {
    const fields: DynamicFieldConfig[] = [];

    for (const [key, rawProp] of Object.entries(properties)) {
      if (excludeKeys.includes(key)) continue;
      const prop = rawProp as Record<string, unknown>;
      fields.push(this.buildFieldConfig(key, prop, required.includes(key)));
    }

    return fields;
  }

  private buildFieldConfig(
    key: string,
    prop: Record<string, unknown>,
    isRequired: boolean,
  ): DynamicFieldConfig {
    const dataType = (prop['type'] as DynamicFieldConfig['dataType']) ?? 'string';
    const enumValues = prop['enum'] as string[] | undefined;
    const uiWidget = (prop['ui:widget'] as UiWidget | undefined) ??
      this.inferWidget(dataType, enumValues);

    const options = enumValues?.map((v) => ({ value: v, label: v }));

    // Determinar valor por defecto con prioridad: prop['default'] -> defaults estándar de la plataforma
    let defaultValue = prop['default'];
    if (defaultValue === undefined || defaultValue === null || defaultValue === '') {
      switch (key) {
        case 'environment':
          defaultValue = 'QA_AZURE';
          break;
        case 'environmentRef':
          defaultValue = 'QA_Oracle';
          break;
        case 'tenant':
          defaultValue = 'BPP KTX SAAS';
          break;
        case 'application':
          defaultValue = 'Koncilia';
          break;
      }
    }

    return {
      key,
      label:        this.humanize(key),
      widget:       uiWidget,
      required:     isRequired,
      defaultValue,
      options,
      placeholder:  prop['ui:placeholder'] as string | undefined,
      hint:         prop['description'] as string | undefined,
      dataType,
      validators: {
        minLength: prop['minLength'] as number | undefined,
        maxLength: prop['maxLength'] as number | undefined,
        min:       prop['minimum'] as number | undefined,
        max:       prop['maximum'] as number | undefined,
        pattern:   prop['pattern'] as string | undefined,
      },
    };
  }

  private buildProcessFormConfig(
    itemSchema: Record<string, unknown>,
    index: number,
  ): ProcessFormConfig {
    const props = (itemSchema['properties'] as Record<string, unknown>) ?? {};
    const required = (itemSchema['required'] as string[]) ?? [];

    // Campos del contexto del proceso
    const contextSchema = (props['context'] as Record<string, unknown>) ?? {};
    const contextProps = (contextSchema['properties'] as Record<string, unknown>) ?? {};
    const contextFields = this.extractFieldsFromProperties(contextProps, []);

    // Agregar processId como campo
    const processIdField: DynamicFieldConfig = {
      key: 'processId', label: 'Process ID', widget: 'text',
      required: required.includes('processId'), dataType: 'string',
      placeholder: 'Ej: Cruce_Tx_Breb', hint: 'Identificador único del proceso',
      validators: {},
    };

    // Configuración de steps — extraer campos comunes y condicionales
    const stepsSchema = (props['steps'] as Record<string, unknown>) ?? {};
    const stepItemSchema = (stepsSchema['items'] as Record<string, unknown>) ?? {};
    const stepConfig = this.buildStepFormConfig(stepItemSchema, 0);

    return {
      processIndex: index,
      contextFields: [processIdField, ...contextFields],
      steps: [stepConfig],
    };
  }

  private buildStepFormConfig(
    stepSchema: Record<string, unknown>,
    index: number,
  ): StepFormConfig {
    const props = (stepSchema['properties'] as Record<string, unknown>) ?? {};
    const required = (stepSchema['required'] as string[]) ?? [];

    // Campos comunes: stepId, actionType, timeoutSeconds, retryCount
    const commonKeys = ['stepId', 'actionType', 'timeoutSeconds', 'retryCount'];
    const commonFields: DynamicFieldConfig[] = commonKeys
      .filter((k) => props[k])
      .map((k) => this.buildFieldConfig(k, props[k] as Record<string, unknown>, required.includes(k)));

    // Forzar actionType como autocomplete con opciones conocidas
    const actionTypeField = commonFields.find((f) => f.key === 'actionType');
    if (actionTypeField) {
      actionTypeField.widget = 'autocomplete';
      actionTypeField.options = [
        { value: 'UI_SEQUENCE',          label: 'UI_SEQUENCE' },
        { value: 'SQL_EXECUTE',          label: 'SQL_EXECUTE' },
        { value: 'ASSERT_BUSINESS_RULES', label: 'ASSERT_BUSINESS_RULES' },
      ];
    }

    // Campos condicionales por actionType (del polimorfismo allOf/if-then)
    const conditionalFields: Record<ActionType, DynamicFieldConfig[]> = {
      'UI_SEQUENCE': [
        { key: 'uiCommands', label: 'UI Commands (JSON)', widget: 'textarea',
          required: true, dataType: 'array', hint: 'Array de comandos UI en formato JSON',
          validators: {} },
      ],
      'SQL_EXECUTE': [
        { key: 'catalog', label: 'Catálogo de BD', widget: 'select',
          required: true, dataType: 'string',
          options: [{ value: 'Oracle', label: 'Oracle' }, { value: 'SQL Server', label: 'SQL Server' }],
          validators: {} },
        { key: 'query', label: 'Consulta SQL', widget: 'textarea',
          required: true, dataType: 'string', placeholder: 'SELECT ... FROM ...', validators: {} },
        { key: 'useIndexFastFullScan', label: 'Usar Index Fast Full Scan', widget: 'checkbox',
          required: false, dataType: 'boolean', defaultValue: false, validators: {} },
      ],
      'ASSERT_BUSINESS_RULES': [
        { key: 'expectedValue', label: 'Valor Esperado', widget: 'number',
          required: true, dataType: 'number', validators: {} },
        { key: 'operator', label: 'Operador', widget: 'select',
          required: true, dataType: 'string',
          options: [
            { value: '>=', label: '>= (Mayor o igual)' },
            { value: '==', label: '== (Igual)' },
            { value: '<=', label: '<= (Menor o igual)' },
          ], validators: {} },
      ],
    };

    return { stepIndex: index, commonFields, conditionalFields };
  }

  // ── CONSTRUCCIÓN DE FORMGROUPS ────────────────────────────────────────────

  private buildProcessFormGroup(config: ProcessFormConfig): FormGroup {
    const controls: Record<string, unknown> = {};

    for (const field of config.contextFields) {
      controls[field.key] = [field.defaultValue ?? '', this.buildValidators(field)];
    }

    controls['steps'] = this.fb.array(
      config.steps.map((s) => this.buildStepFormGroup(s)),
    );

    return this.fb.group(controls);
  }

  private buildStepFormGroup(config: StepFormConfig): FormGroup {
    const controls: Record<string, unknown> = {};

    for (const field of config.commonFields) {
      controls[field.key] = [field.defaultValue ?? '', this.buildValidators(field)];
    }

    // Payload como subgrupo vacío (se pobla dinámicamente por actionType)
    controls['payload'] = this.fb.group({});

    return this.fb.group(controls);
  }

  // ── UTILIDADES ────────────────────────────────────────────────────────────

  private buildValidators(field: DynamicFieldConfig): ValidatorFn[] {
    const validators: ValidatorFn[] = [];
    if (field.required) validators.push(Validators.required);
    if (field.validators?.minLength) validators.push(Validators.minLength(field.validators.minLength));
    if (field.validators?.maxLength) validators.push(Validators.maxLength(field.validators.maxLength));
    if (field.validators?.min != null) validators.push(Validators.min(field.validators.min));
    if (field.validators?.max != null) validators.push(Validators.max(field.validators.max));
    if (field.validators?.pattern) validators.push(Validators.pattern(field.validators.pattern));
    return validators;
  }

  private inferWidget(
    dataType: DynamicFieldConfig['dataType'],
    enumValues?: string[],
  ): UiWidget {
    if (enumValues?.length) return 'select';
    if (dataType === 'number' || dataType === 'integer') return 'number';
    if (dataType === 'boolean') return 'checkbox';
    return 'text';
  }

  /** Convierte camelCase o snake_case a "Texto Legible" */
  private humanize(key: string): string {
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/^\w/, (c) => c.toUpperCase())
      .trim();
  }
}
