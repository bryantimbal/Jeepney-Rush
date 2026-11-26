-- App updates: the newest release on Google Play and the oldest version still allowed.
-- The game asks Google Play first (In-App Updates); this is the backup check (and the only way to
-- REQUIRE an update). After each release, update the row in the Table Editor or with SQL:
--   update public.jr_app_release set latest_code = 12, latest_name = '1.2.0', message = 'Bagong Ghost Race!' where id = 1;
-- Raise min_code only when old versions must stop (e.g. after a server change they can't handle).

create table if not exists public.jr_app_release (
  id int primary key default 1 check (id = 1),
  latest_code int not null default 0,
  min_code int not null default 0,
  latest_name text not null default '',
  message text not null default '',
  updated_at timestamptz not null default now());
insert into public.jr_app_release (id) values (1) on conflict (id) do nothing;
alter table public.jr_app_release enable row level security;
revoke all on public.jr_app_release from anon, authenticated;

-- readable without signing in (the check runs before sign-in)
create or replace function public.jr_app_version() returns jsonb
language sql stable security definer set search_path = public as $$
  select jsonb_build_object('latest_code', latest_code, 'min_code', min_code, 'latest_name', latest_name, 'message', message)
  from public.jr_app_release where id = 1
$$;
revoke all on function public.jr_app_version() from public;
grant execute on function public.jr_app_version() to anon, authenticated;
