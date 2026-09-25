import { useCallback, useEffect, useState } from 'react';

// Executa uma chamada da API e expõe { data, error, loading, reload }.
export function useRequest(fn) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    return fn()
      .then(setData)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
    // fn é sempre uma função estável de `api`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { data, error, loading, reload: load };
}
