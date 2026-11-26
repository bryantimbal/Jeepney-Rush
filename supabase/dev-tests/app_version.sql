-- App version check: readable by anyone (no sign-in needed), only the owner can change it.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
update public.jr_app_release set latest_code = 12, min_code = 3, latest_name = '1.2.0', message = 'Bagong update' where id = 1;
set role anon;
do $$ declare v jsonb; begin
  v := public.jr_app_version();
  assert (v->>'latest_code')::int = 12 and (v->>'min_code')::int = 3 and v->>'latest_name' = '1.2.0', 'readable signed out: ' || v::text;
  begin update public.jr_app_release set min_code = 0; assert false, 'players must not change it';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  app version: public read-only check';
end $$;
reset role;
