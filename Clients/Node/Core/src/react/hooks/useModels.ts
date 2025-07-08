import { useQuery } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import { conduitQueryKeys } from '../queryKeys';

export function useModels() {
  const { client } = useConduit();

  return useQuery({
    queryKey: conduitQueryKeys.models(),
    queryFn: async () => {
      return await client.models.list();
    },
  });
}