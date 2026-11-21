-- Minimal stand-in for the parts of Supabase the migration relies on, so the backend can be
-- tested on a plain local Postgres: the anon/authenticated/service_role roles, auth.users
-- and auth.uid() (which Supabase reads from the request JWT).
create role anon nologin;
create role authenticated nologin;
create role service_role nologin bypassrls;
create schema auth;
create table auth.users (
  id uuid primary key default gen_random_uuid(),
  email text,
  is_anonymous boolean not null default false,
  created_at timestamptz not null default now());
create function auth.uid() returns uuid language sql stable as $$
  select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
grant usage on schema auth to anon, authenticated;
grant execute on function auth.uid() to anon, authenticated;
grant usage on schema public to anon, authenticated;
-- Supabase grants EXECUTE on new public functions to anon/authenticated by default; mimic that
-- so the migration's REVOKEs are actually exercised.
alter default privileges in schema public grant execute on functions to anon, authenticated;
