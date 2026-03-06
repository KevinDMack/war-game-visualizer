{{/*
Expand the name of the chart.
*/}}
{{- define "war-game-visualizer.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "war-game-visualizer.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "war-game-visualizer.labels" -}}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Selector labels for the web-app
*/}}
{{- define "war-game-visualizer.webApp.selectorLabels" -}}
app.kubernetes.io/name: web-app
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Selector labels for the scenario-service
*/}}
{{- define "war-game-visualizer.scenarioService.selectorLabels" -}}
app.kubernetes.io/name: scenario-service
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
