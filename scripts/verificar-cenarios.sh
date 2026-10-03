#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# verificar-cenarios.sh — Verificação automatizada dos 6 cenários do lab.
#
# Roda cada cenário nos dois estados (vulnerável / corrigido), imprime uma
# tabela de evidência e restaura o lab ao estado seguro no finally.
#
# Regras de segurança:
#   - Nunca toca nas 6 contas do seed.
#   - Limpa apenas o que ele mesmo cria: DELETE ... WHERE "Username" ~ '^probe[0-9]+$'.
#   - Espera a janela de rate limit antes de conferir logins.
#   - verbose-errors roda por último (esgota o rate limit de login).
#   - Idempotente: rodar duas vezes seguidas termina com 6 usuários e lab seguro.
# ---------------------------------------------------------------------------
set -euo pipefail

API="${API_URL:-http://localhost:5000}"
DB_CONTAINER="${DB_CONTAINER:-cyberprotech-security-lab-db-1}"
DB_NAME="${POSTGRES_DB:-cyberprotech_lab}"
DB_USER="${POSTGRES_USER:-postgres}"

# Cores para a saída
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# ---------------------------------------------------------------------------
# Estado global de resultados (preenchido por cada cenário)
# ---------------------------------------------------------------------------
declare -a RESULTADO_CENARIO=()
declare -a RESULTADO_VULN=()
declare -a RESULTADO_FIX=()
declare -a RESULTADO_OK=()
FALHAS=0

# ---------------------------------------------------------------------------
# Funções auxiliares
# ---------------------------------------------------------------------------
log()  { echo -e "${CYAN}[INFO]${NC} $*"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
err()  { echo -e "${RED}[ERRO]${NC} $*"; }
ok()   { echo -e "${GREEN}[OK]${NC} $*"; }

# Obtém a senha do seed a partir do .env
obter_seed_password() {
  local env_file
  env_file="$(dirname "$(readlink -f "$0")")/../.env"
  if [[ ! -f "$env_file" ]]; then
    env_file=".env"
  fi
  if [[ ! -f "$env_file" ]]; then
    err "Arquivo .env não encontrado"
    exit 1
  fi
  grep '^SEED_PASSWORD=' "$env_file" | cut -d= -f2-
}

SEED_PASSWORD="$(obter_seed_password)"
if [[ -z "$SEED_PASSWORD" ]]; then
  err "SEED_PASSWORD vazio no .env"
  exit 1
fi

# Login e extração do token
fazer_login() {
  local user="$1" senha="$2"
  curl -s -X POST "${API}/api/auth/login" \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"${user}\",\"password\":\"${senha}\"}" \
    | sed -n 's/.*"token":"\([^"]*\)".*/\1/p'
}

# Altera controles do lab
lab_config() {
  curl -s -X PUT "${API}/api/lab/config" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H 'Content-Type: application/json' \
    -d "$1" > /dev/null
}

# Restaura o lab ao estado seguro (padrão)
restaurar_lab() {
  log "Restaurando o lab ao estado seguro..."
  # Precisa de um token válido; se o atual expirou, tenta relogar
  local t
  t="$(fazer_login admin "$SEED_PASSWORD")"
  if [[ -n "$t" ]]; then
    TOKEN="$t"
  fi
  lab_config '{"vulnMode":false,"verboseErrors":false,"rateLimit":true,"securityHeaders":true}'
}

# Limpa apenas os usuários criados por este script (padrão ancorado)
limpar_probes() {
  log "Limpando usuários probe..."
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c \
    "DELETE FROM users WHERE \"Username\" ~ '^probe[0-9]+\$';" 2>/dev/null || true
}

# Conta os usuários no banco
contar_usuarios() {
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -tA -c \
    "SELECT COUNT(*) FROM users;" 2>/dev/null | tr -d '[:space:]'
}

# Registra resultado de um cenário
registrar() {
  local cenario="$1" evidencia_vuln="$2" evidencia_fix="$3" sucesso="$4"
  RESULTADO_CENARIO+=("$cenario")
  RESULTADO_VULN+=("$evidencia_vuln")
  RESULTADO_FIX+=("$evidencia_fix")
  RESULTADO_OK+=("$sucesso")
  if [[ "$sucesso" != "OK" ]]; then
    FALHAS=$((FALHAS + 1))
  fi
}

# ---------------------------------------------------------------------------
# Trap: restaura o lab mesmo se o script falhar no meio
# ---------------------------------------------------------------------------
cleanup() {
  restaurar_lab
  limpar_probes
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Pré-verificação: API no ar?
# ---------------------------------------------------------------------------
log "Verificando se a API está acessível em ${API}..."
if ! curl -sf "${API}/api/health" > /dev/null 2>&1; then
  err "API não responde em ${API}/api/health"
  exit 1
fi
ok "API acessível"

# ---------------------------------------------------------------------------
# Limpar probes de execuções anteriores
# ---------------------------------------------------------------------------
limpar_probes

# ---------------------------------------------------------------------------
# Login como admin
# ---------------------------------------------------------------------------
log "Autenticando como admin..."
TOKEN="$(fazer_login admin "$SEED_PASSWORD")"
if [[ -z "$TOKEN" ]]; then
  err "Falha ao obter token de admin. Verifique SEED_PASSWORD e o estado da API."
  exit 1
fi
ok "Token obtido"

# Login como aluno01 (para IDOR)
log "Autenticando como aluno01..."
TOKEN_ALUNO="$(fazer_login aluno01 "$SEED_PASSWORD")"
if [[ -z "$TOKEN_ALUNO" ]]; then
  err "Falha ao obter token de aluno01"
  exit 1
fi
ok "Token do aluno01 obtido"

# ID do admin (para IDOR)
ADMIN_ID="3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01"

# ===================================================================
# CENÁRIO 1 — SQL Injection (vuln-mode)
# ===================================================================
echo ""
log "${BOLD}Cenário 1: SQL Injection${NC}"

# Estado vulnerável
lab_config '{"vulnMode":true}'
sleep 0.3
sqli_vuln=$(curl -s -H "Authorization: Bearer $TOKEN" \
  "${API}/api/users?search=%27%20OR%20%271%27%3D%271" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d.get('items',d) if isinstance(d,dict) else d))" 2>/dev/null || echo "erro")

# Estado corrigido
lab_config '{"vulnMode":false}'
sleep 0.3
sqli_fix=$(curl -s -H "Authorization: Bearer $TOKEN" \
  "${API}/api/users?search=%27%20OR%20%271%27%3D%271" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d.get('items',d) if isinstance(d,dict) else d))" 2>/dev/null || echo "erro")

sqli_ok="FALHA"
if [[ "$sqli_vuln" =~ ^[0-9]+$ ]] && (( sqli_vuln >= 6 )) && [[ "$sqli_fix" == "0" ]]; then
  sqli_ok="OK"
  ok "SQLi: vulnerável=${sqli_vuln} linhas, corrigido=${sqli_fix} linhas"
else
  err "SQLi: vulnerável=${sqli_vuln}, corrigido=${sqli_fix}"
fi
registrar "SQL Injection" "${sqli_vuln} linhas" "${sqli_fix} linhas" "$sqli_ok"

# ===================================================================
# CENÁRIO 2 — XSS armazenado (vuln-mode)
# ===================================================================
echo ""
log "${BOLD}Cenário 2: XSS armazenado${NC}"

XSS_PAYLOAD='<script>alert(1)</script>'

# Criar um usuário probe para XSS
probe_xss_user="probe1"
probe_xss_email="probe1@lab.invalid"
probe_xss_resp=$(curl -s -X POST "${API}/api/auth/register" \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"${probe_xss_user}\",\"email\":\"${probe_xss_email}\",\"password\":\"Probe1xss!\"}")
probe_xss_token=$(echo "$probe_xss_resp" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

if [[ -z "$probe_xss_token" ]]; then
  err "Falha ao criar probe1 para XSS"
  registrar "XSS armazenado" "erro ao criar probe" "-" "FALHA"
else
  # Estado vulnerável
  lab_config '{"vulnMode":true}'
  sleep 0.3
  curl -s -X PUT "${API}/api/auth/me" \
    -H "Authorization: Bearer $probe_xss_token" -H 'Content-Type: application/json' \
    -d "{\"email\":\"${probe_xss_email}\",\"bio\":\"${XSS_PAYLOAD}\"}" > /dev/null

  xss_vuln_bio=$(curl -s -H "Authorization: Bearer $probe_xss_token" "${API}/api/auth/me" | \
    python3 -c "import sys,json; print(json.load(sys.stdin).get('bio',''))" 2>/dev/null || echo "erro")

  # Estado corrigido: reescrever a bio
  lab_config '{"vulnMode":false}'
  sleep 0.3
  curl -s -X PUT "${API}/api/auth/me" \
    -H "Authorization: Bearer $probe_xss_token" -H 'Content-Type: application/json' \
    -d "{\"email\":\"${probe_xss_email}\",\"bio\":\"${XSS_PAYLOAD}\"}" > /dev/null

  xss_fix_bio=$(curl -s -H "Authorization: Bearer $probe_xss_token" "${API}/api/auth/me" | \
    python3 -c "import sys,json; print(json.load(sys.stdin).get('bio',''))" 2>/dev/null || echo "erro")

  xss_ok="FALHA"
  xss_vuln_desc="HTML cru"
  xss_fix_desc="tags removidas"
  if echo "$xss_vuln_bio" | grep -q '<script>'; then
    xss_vuln_desc="HTML cru (payload intacto)"
  else
    xss_vuln_desc="inesperado: ${xss_vuln_bio}"
  fi
  if ! echo "$xss_fix_bio" | grep -q '<script>'; then
    xss_fix_desc="tags removidas"
    if echo "$xss_vuln_bio" | grep -q '<script>'; then
      xss_ok="OK"
    fi
  else
    xss_fix_desc="FALHA: tags não removidas"
  fi

  if [[ "$xss_ok" == "OK" ]]; then
    ok "XSS: vulnerável=payload intacto, corrigido=tags removidas"
  else
    err "XSS: vulnerável=${xss_vuln_bio}, corrigido=${xss_fix_bio}"
  fi
  registrar "XSS armazenado" "$xss_vuln_desc" "$xss_fix_desc" "$xss_ok"
fi

# ===================================================================
# CENÁRIO 3 — IDOR / Broken Access Control (vuln-mode)
# ===================================================================
echo ""
log "${BOLD}Cenário 3: IDOR / Broken Access Control${NC}"

# Estado vulnerável
lab_config '{"vulnMode":true}'
sleep 0.3
idor_vuln_status=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN_ALUNO" \
  "${API}/api/users/${ADMIN_ID}")

# Estado corrigido
lab_config '{"vulnMode":false}'
sleep 0.3
idor_fix_status=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN_ALUNO" \
  "${API}/api/users/${ADMIN_ID}")

idor_ok="FALHA"
if [[ "$idor_vuln_status" == "200" ]] && [[ "$idor_fix_status" == "403" ]]; then
  idor_ok="OK"
  ok "IDOR: vulnerável=200, corrigido=403"
else
  err "IDOR: vulnerável=${idor_vuln_status}, corrigido=${idor_fix_status}"
fi
registrar "IDOR" "200 (dados do admin)" "403" "$idor_ok"

# ===================================================================
# CENÁRIO 4 — Configuração insegura / Security Headers (sec-headers)
# ===================================================================
echo ""
log "${BOLD}Cenário 4: Configuração insegura (Security Headers)${NC}"

# Estado vulnerável (headers desligados)
lab_config '{"securityHeaders":false}'
sleep 0.3
headers_vuln=$(curl -sI "${API}/api/health" | grep -ciE '^(Content-Security-Policy|Strict-Transport-Security|X-Content-Type-Options|X-Frame-Options|Referrer-Policy|X-CyberProtech-Lab):' || true)

# Estado corrigido (headers ligados)
lab_config '{"securityHeaders":true}'
sleep 0.3
headers_fix=$(curl -sI "${API}/api/health" | grep -ciE '^(Content-Security-Policy|Strict-Transport-Security|X-Content-Type-Options|X-Frame-Options|Referrer-Policy|X-CyberProtech-Lab):' || true)

headers_ok="FALHA"
if (( headers_vuln == 0 )) && (( headers_fix >= 5 )); then
  headers_ok="OK"
  ok "Headers: vulnerável=${headers_vuln} cabeçalhos, corrigido=${headers_fix} cabeçalhos"
else
  err "Headers: vulnerável=${headers_vuln}, corrigido=${headers_fix}"
fi
registrar "Config insegura" "${headers_vuln} cabeçalhos" "${headers_fix} cabeçalhos" "$headers_ok"

# ===================================================================
# CENÁRIO 5 — Falhas de autenticação / Rate Limit (rate-limit)
# ===================================================================
echo ""
log "${BOLD}Cenário 5: Falhas de autenticação (Rate Limit)${NC}"

# Esperar a janela de rate limit expirar (60 segundos) para garantir
# que tentativas anteriores não contaminem este teste.
# Primeiro verificar se há espaço na janela atual.
log "Aguardando a janela de rate limit expirar (65s)..."
sleep 65

# Estado vulnerável (rate limit desligado)
lab_config '{"rateLimit":false}'
sleep 0.3

rate_vuln_429=0
rate_vuln_401=0
for i in $(seq 1 14); do
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${API}/api/auth/login" \
    -H 'Content-Type: application/json' -d '{"username":"admin","password":"errada"}')
  if [[ "$code" == "429" ]]; then
    rate_vuln_429=$((rate_vuln_429 + 1))
  elif [[ "$code" == "401" ]]; then
    rate_vuln_401=$((rate_vuln_401 + 1))
  fi
done

# Esperar a janela expirar antes de testar o estado corrigido
log "Aguardando a janela de rate limit expirar (65s)..."
sleep 65

# Estado corrigido (rate limit ligado)
lab_config '{"rateLimit":true}'
sleep 0.3

rate_fix_429=0
rate_fix_401=0
for i in $(seq 1 14); do
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${API}/api/auth/login" \
    -H 'Content-Type: application/json' -d '{"username":"admin","password":"errada"}')
  if [[ "$code" == "429" ]]; then
    rate_fix_429=$((rate_fix_429 + 1))
  elif [[ "$code" == "401" ]]; then
    rate_fix_401=$((rate_fix_401 + 1))
  fi
done

rate_ok="FALHA"
if (( rate_vuln_429 == 0 )) && (( rate_fix_429 > 0 )); then
  rate_ok="OK"
  ok "Rate limit: vulnerável=0×429, corrigido=${rate_fix_429}×429"
else
  err "Rate limit: vulnerável=${rate_vuln_429}×429, corrigido=${rate_fix_429}×429"
fi
registrar "Falhas de autenticação" "${rate_vuln_401}×401, ${rate_vuln_429}×429" "${rate_fix_401}×401, ${rate_fix_429}×429" "$rate_ok"

# ===================================================================
# CENÁRIO 6 — Exposição de informações / Verbose Errors (verbose-errors)
# (Roda por ÚLTIMO — esgota o rate limit de login)
# ===================================================================
echo ""
log "${BOLD}Cenário 6: Exposição de informações (Verbose Errors)${NC}"

# Esperar a janela de rate limit expirar antes de verbose-errors
log "Aguardando a janela de rate limit expirar (65s)..."
sleep 65

# Estado vulnerável (verbose-errors ligado)
lab_config '{"verboseErrors":true}'
sleep 0.3

probe_suffix=2
probe_payload="{\"username\":\"probe${probe_suffix}\",\"email\":\"probe${probe_suffix}@lab.invalid\",\"password\":\"Toolkit1!\"}"

# Duas requisições paralelas para provocar violação de unicidade
verbose_bodies=""
for i in 1 2; do
  curl -sS -X POST "${API}/api/auth/register" \
    -H 'Content-Type: application/json' -d "$probe_payload" > "/tmp/verbose_resp_${i}.txt" 2>/dev/null &
done
wait
verbose_bodies="$(cat /tmp/verbose_resp_1.txt /tmp/verbose_resp_2.txt 2>/dev/null)"
rm -f /tmp/verbose_resp_1.txt /tmp/verbose_resp_2.txt

verbose_vuln="genérico"
if echo "$verbose_bodies" | grep -qiE '(stackTrace|stack_trace|"exception")'; then
  verbose_vuln="exception + stackTrace"
fi

# Estado corrigido (verbose-errors desligado)
lab_config '{"verboseErrors":false}'
sleep 0.3

# Limpar o probe2 para poder repetir
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c \
  "DELETE FROM users WHERE \"Username\" ~ '^probe[0-9]+\$';" 2>/dev/null || true

# Recriar probe para teste corrigido
probe_suffix=3
probe_payload="{\"username\":\"probe${probe_suffix}\",\"email\":\"probe${probe_suffix}@lab.invalid\",\"password\":\"Toolkit1!\"}"

for i in 1 2; do
  curl -sS -X POST "${API}/api/auth/register" \
    -H 'Content-Type: application/json' -d "$probe_payload" > "/tmp/verbose_fix_${i}.txt" 2>/dev/null &
done
wait
verbose_fix_bodies="$(cat /tmp/verbose_fix_1.txt /tmp/verbose_fix_2.txt 2>/dev/null)"
rm -f /tmp/verbose_fix_1.txt /tmp/verbose_fix_2.txt

verbose_fix="genérico"
if echo "$verbose_fix_bodies" | grep -qiE '(stackTrace|stack_trace|"exception")'; then
  verbose_fix="exception exposta"
fi

verbose_ok="FALHA"
if [[ "$verbose_vuln" == "exception + stackTrace" ]] && [[ "$verbose_fix" == "genérico" ]]; then
  verbose_ok="OK"
  ok "Verbose errors: vulnerável=stackTrace exposto, corrigido=genérico"
else
  err "Verbose errors: vulnerável=${verbose_vuln}, corrigido=${verbose_fix}"
fi
registrar "Exposição de informações" "$verbose_vuln" "$verbose_fix" "$verbose_ok"

# ===================================================================
# TABELA FINAL DE EVIDÊNCIA
# ===================================================================
echo ""
echo -e "${BOLD}╔══════════════════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}║                    TABELA DE EVIDÊNCIA — 6 CENÁRIOS                        ║${NC}"
echo -e "${BOLD}╠═══════════════════════════╦════════════════════════╦══════════════════╦═════╣${NC}"
printf  "${BOLD}║ %-25s ║ %-22s ║ %-16s ║ %-3s ║${NC}\n" "Cenário" "Vulnerável" "Corrigido" "   "
echo -e "${BOLD}╠═══════════════════════════╬════════════════════════╬══════════════════╬═════╣${NC}"

for i in "${!RESULTADO_CENARIO[@]}"; do
  status_color="${GREEN}"
  if [[ "${RESULTADO_OK[$i]}" != "OK" ]]; then
    status_color="${RED}"
  fi
  printf "║ %-25s ║ %-22s ║ %-16s ║ ${status_color}%-3s${NC} ║\n" \
    "${RESULTADO_CENARIO[$i]}" "${RESULTADO_VULN[$i]}" "${RESULTADO_FIX[$i]}" "${RESULTADO_OK[$i]}"
done

echo -e "${BOLD}╚═══════════════════════════╩════════════════════════╩══════════════════╩═════╝${NC}"

# ===================================================================
# VERIFICAÇÃO FINAL: contagem de usuários
# ===================================================================
echo ""
log "Verificando contagem de usuários no banco..."
# O cleanup do trap vai limpar os probes, mas vamos limpar aqui também
# para contar corretamente
limpar_probes

CONTAGEM=$(contar_usuarios)
echo -e "Usuários no banco: ${BOLD}${CONTAGEM}${NC}"

if [[ "$CONTAGEM" != "6" ]]; then
  err "Contagem esperada: 6. Contagem real: ${CONTAGEM}."
  err "O seed pode estar corrompido. Verifique manualmente."
  exit 1
fi
ok "Contagem de usuários correta: 6"

# ===================================================================
# RESULTADO FINAL
# ===================================================================
echo ""
if (( FALHAS > 0 )); then
  err "${FALHAS} cenário(s) com falha."
  exit 1
else
  ok "Todos os 6 cenários verificados com sucesso."
  exit 0
fi
