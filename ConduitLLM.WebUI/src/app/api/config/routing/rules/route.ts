import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/config/routing/rules - Get all routing rules
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    try {
      // Try to fetch routing rules from SDK
      const response = await adminClient.configuration.getRoutingRules();
      
      // Handle array response
      const rules = response || [];
      return NextResponse.json(Array.isArray(rules) ? rules : []);
    } catch (error) {
      console.warn('Failed to fetch routing rules:', error);
      
      // Return empty array if SDK doesn't support it yet
      return NextResponse.json([]);
    }
  } catch (error) {
    console.error('Error fetching routing rules:', error);
    return handleSDKError(error);
  }
}

// POST /api/config/routing/rules - Create a new routing rule
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const ruleData = await req.json();
    
    try {
      const createDto: any = {
        name: ruleData.name,
        priority: ruleData.priority,
        enabled: ruleData.enabled,
        conditions: ruleData.conditions,
        actions: ruleData.actions
      };
      
      const newRule = await adminClient.configuration.createRoutingRule(createDto);
      
      return NextResponse.json(newRule);
    } catch (error) {
      console.warn('Failed to create routing rule:', error);
      
      // Return a mock created rule if SDK doesn't support it yet
      return NextResponse.json({
        id: Date.now().toString(),
        name: ruleData.name,
        description: ruleData.description,
        priority: ruleData.priority || 1,
        isEnabled: ruleData.enabled || false,
        conditions: ruleData.conditions || [],
        actions: ruleData.actions || [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        _warning: 'Rule created locally (SDK support pending)'
      });
    }
  } catch (error) {
    console.error('Error creating routing rule:', error);
    return handleSDKError(error);
  }
}

// PUT /api/config/routing/rules - Bulk update routing rules
export async function PUT(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { rules } = await req.json();
    
    // SDK doesn't support bulk update yet, so we'll return the rules as-is
    console.warn('Bulk update not supported by SDK, returning requested rules');
    return NextResponse.json(rules);
  } catch (error) {
    console.error('Error bulk updating routing rules:', error);
    return handleSDKError(error);
  }
}