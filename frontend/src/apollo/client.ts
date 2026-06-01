import {
  ApolloClient,
  InMemoryCache,
  HttpLink,
  ApolloLink,
  split,
} from '@apollo/client'
import { GraphQLWsLink } from '@apollo/client/link/subscriptions'
import { getMainDefinition } from '@apollo/client/utilities'
import { createClient } from 'graphql-ws'
import { getTokenRef } from './AuthContext'

// Injects Authorization header when a token is available
const authLink = new ApolloLink((operation, forward) => {
  const token = getTokenRef()
  if (token) {
    operation.setContext({
      headers: { Authorization: `Bearer ${token}` },
    })
  }
  return forward(operation)
})

// HTTP link for queries and mutations — Vite proxy forwards /graphql → API
const httpLink = new HttpLink({ uri: '/graphql' })

// WebSocket link for subscriptions
const wsLink = new GraphQLWsLink(
  createClient({
    url: `ws://${window.location.host}/graphql`,
    connectionParams: () => {
      const token = getTokenRef()
      return token ? { Authorization: `Bearer ${token}` } : {}
    },
  })
)

// Route subscriptions over WS, everything else over HTTP
const splitLink = split(
  ({ query }) => {
    const def = getMainDefinition(query)
    return def.kind === 'OperationDefinition' && def.operation === 'subscription'
  },
  wsLink,
  authLink.concat(httpLink)
)

export const apolloClient = new ApolloClient({
  link: splitLink,
  cache: new InMemoryCache(),
  defaultOptions: {
    watchQuery: { fetchPolicy: 'cache-and-network' },
  },
})
